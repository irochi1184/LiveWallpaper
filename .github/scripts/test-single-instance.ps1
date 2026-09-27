param([Parameter(Mandatory = $true)][string]$AppPath)
$ErrorActionPreference = 'Stop'
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
    if (-not $process.CloseMainWindow()) { throw 'Could not request normal app shutdown.' }
    if (-not $process.WaitForExit(15000)) { throw 'App did not exit after closing its main window.' }
}
try {
    $primary = Start-TestApp
    $null = Wait-SingleWindow
    $secondary = Start-TestApp
    if (-not $secondary.WaitForExit(30000)) { throw 'Second launch did not redirect and exit.' }
    $primary.Refresh()
    if ($primary.HasExited -or $primary.MainWindowHandle -eq 0) { throw 'Primary window was lost.' }
    Close-TestApp $primary
    foreach ($process in $started) { $process.Dispose() }
    $started.Clear()

    # Simultaneous cold launches exercise registration before MainWindow exists.
    $null = Start-TestApp
    $null = Start-TestApp
    $survivor = Wait-SingleWindow
    Close-TestApp $survivor
    Write-Output 'PASS: second launch redirects/exits; cold concurrent launches leave one window; normal close exits'
}
finally {
    foreach ($process in $started) {
        $process.Refresh()
        if (-not $process.HasExited) { $process.Kill(); $null = $process.WaitForExit(5000) }
        $process.Dispose()
    }
}
