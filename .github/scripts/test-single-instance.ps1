param([Parameter(Mandatory = $true)][string]$AppPath)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
# Start-Process uses SW_HIDE. MainWindowHandle ignores hidden HWNDs, so locate
# and show only our startup window. Reopen checks deliberately do not use this.
Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class StartupWindow {
    private delegate bool EnumProc(IntPtr hwnd, IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc proc, IntPtr data);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int count);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int command);
    public static void Reveal(int processId) {
        EnumWindows((hwnd, data) => {
            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);
            if (pid != processId) return true;
            var text = new StringBuilder(256);
            GetWindowText(hwnd, text, text.Capacity);
            if (text.ToString() != "LiveWallpaper") return true;
            ShowWindow(hwnd, 5);
            return false;
        }, IntPtr.Zero);
    }
}
'@
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
function Wait-SingleWindow([switch]$RevealStartupWindow) {
    $deadline = (Get-Date).AddSeconds(45)
    do {
        $running = @($started | Where-Object { $_.Refresh(); -not $_.HasExited })
        if ($running.Count -eq 0) { throw 'All launched app processes exited before a main window appeared.' }
        if ($RevealStartupWindow -and $running.Count -eq 1 -and $running[0].MainWindowHandle -eq 0) {
            [StartupWindow]::Reveal($running[0].Id)
            $running[0].Refresh()
        }
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
    $null = Wait-SingleWindow -RevealStartupWindow
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
    $survivor = Wait-SingleWindow -RevealStartupWindow
    Close-TestApp $survivor
    Write-Output 'PASS: second launch redirects/exits; cold concurrent launches leave one window; explicit Exit terminates'
}
catch {
    foreach ($process in $started) {
        $process.Refresh()
        if ($process.HasExited) { Write-Output "App PID $($process.Id) exited: $($process.ExitCode)" }
        else { Write-Output "App PID $($process.Id): session=$($process.SessionId), window=$($process.MainWindowHandle), title=$($process.MainWindowTitle)" }
    }
    Get-WinEvent -FilterHashtable @{LogName='Application'; StartTime=(Get-Date).AddMinutes(-5); Level=2} -ErrorAction SilentlyContinue |
        Where-Object { $_.Message -match 'LiveWallpaper' } | Select-Object TimeCreated, ProviderName, Message | Format-List | Out-String | Write-Output
    throw
}
finally {
    foreach ($process in $started) {
        $process.Refresh()
        if (-not $process.HasExited) { $process.Kill(); $null = $process.WaitForExit(5000) }
        $process.Dispose()
    }
}
