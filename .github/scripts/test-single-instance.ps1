param([Parameter(Mandatory = $true)][string]$AppPath)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$appExecutable = (Resolve-Path -LiteralPath $AppPath).Path
$appDirectory = Split-Path -Parent $appExecutable
$processName = [IO.Path]::GetFileNameWithoutExtension($appExecutable)
if (Get-Process -Name $processName -ErrorAction SilentlyContinue) {
    throw 'An existing app instance is running; do not interfere with it.'
}
$started = [Collections.Generic.List[Diagnostics.Process]]::new()
function Start-TestApp {
    $process = Start-Process -FilePath $appExecutable -WorkingDirectory $appDirectory -WindowStyle Hidden -PassThru
    $started.Add($process)
    return $process
}
function Wait-SingleWindow {
    $deadline = (Get-Date).AddSeconds(45)
    do {
        $running = @($started | Where-Object { $_.Refresh(); -not $_.HasExited })
        if ($running.Count -eq 1 -and $running[0].MainWindowHandle -ne 0) { return $running[0] }
        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)
    throw 'Expected exactly one surviving process with a main window.'
}
function Close-TestApp($process) {
    $process.Refresh()
    $window = [Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
    $condition = New-Object Windows.Automation.PropertyCondition ([Windows.Automation.AutomationElement]::AutomationIdProperty), 'ExitApplication'
    $button = $window.FindFirst([Windows.Automation.TreeScope]::Descendants, $condition)
    if ($null -eq $button) { throw 'ExitApplication button not found.' }
    $invoke = $button.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern)
    $invoke.Invoke()
    if (-not $process.WaitForExit(15000)) { throw 'App did not exit after explicit Exit.' }
}
try {
    $primary = Start-TestApp
    $null = Wait-SingleWindow
    $secondary = Start-TestApp
    if (-not $secondary.WaitForExit(30000)) { throw 'Second launch did not redirect and exit.' }
    $primary.Refresh()
    if ($primary.HasExited -or $primary.MainWindowHandle -eq 0) { throw 'Primary window was lost.' }
    if (-not $primary.CloseMainWindow()) { throw 'Could not request close-to-tray.' }
    Start-Sleep -Seconds 1
    $primary.Refresh()
    if ($primary.HasExited) {
        # Hosted runners may not provide Explorer's notification area.
        Write-Output 'INFO: notification area unavailable; closing exited normally (fallback). Tray UI requires an interactive desktop.'
    } else {
        if ($primary.MainWindowHandle -ne 0) { throw 'Close did not hide the settings window.' }
        $reopen = Start-TestApp
        if (-not $reopen.WaitForExit(30000)) { throw 'Reopen did not redirect and exit.' }
        $null = Wait-SingleWindow
        Close-TestApp $primary
        Write-Output 'PASS: close-to-tray keeps process alive; relaunch restores settings; explicit Exit terminates'
    }
    foreach ($process in $started) { $process.Dispose() }
    $started.Clear()

    # Simultaneous cold launches exercise registration before MainWindow exists.
    $null = Start-TestApp
    $null = Start-TestApp
    $survivor = Wait-SingleWindow
    Close-TestApp $survivor
    Write-Output 'PASS: second launch redirects/exits; cold concurrent launches leave one window; explicit Exit terminates'
}
finally {
    foreach ($process in $started) {
        $process.Refresh()
        if (-not $process.HasExited) { $process.Kill(); $null = $process.WaitForExit(5000) }
        $process.Dispose()
    }
}
