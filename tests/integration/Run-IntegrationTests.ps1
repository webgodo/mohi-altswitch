<#
.SYNOPSIS
  End-to-end test: starts the real app, opens three throwaway windows, injects real Alt+` keystrokes
  from outside the app and checks which window ends up in front.

.DESCRIPTION
  This drives a LIVE desktop, so it is deliberately careful:
    * it never sends a key unless one of the throwaway SWTEST windows has the focus, and aborts otherwise;
    * it captures only the popup's own rectangle, never the rest of your screen;
    * if something else grabs focus before any key is sent, it retries with fresh windows.
  Don't type or click elsewhere while it runs (about 20 seconds).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$AppExe,
    [Parameter(Mandatory)][string]$TestWindowsExe,
    [string]$OutDir = (Join-Path $env:TEMP 'MohiAltSwitchTests'),
    [int]$Attempts = 3
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if (-not ('K' -as [type])) {
Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class K {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string title);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
  public static string Fg() { var s = new StringBuilder(256); GetWindowText(GetForegroundWindow(), s, 256); return s.ToString(); }
}
'@
}

$ALT = 0x12; $TICK = 0xC0; $ESC = 0x1B
function Down($vk, $sc) { [K]::keybd_event($vk, $sc, 0, [UIntPtr]::Zero) }
function Up($vk, $sc)   { [K]::keybd_event($vk, $sc, 2, [UIntPtr]::Zero) }
function Tap($vk, $sc)  { Down $vk $sc; Up $vk $sc }
function Guard { if (-not [K]::Fg().StartsWith('SWTEST')) { throw "ABORT: foreground is '$([K]::Fg())', not a test window - no key sent" } }
function Find($title) { [K]::FindWindow([NullString]::Value, $title) }
function PopupUp { $h = Find 'MohiAltSwitchPopup'; return ($h -ne [IntPtr]::Zero -and [K]::IsWindowVisible($h)) }

function ShotPopup($file) {   # only the popup's rectangle
    $h = Find 'MohiAltSwitchPopup'; if ($h -eq [IntPtr]::Zero) { return 'no popup window' }
    $r = New-Object K+RECT; [void][K]::GetWindowRect($h, [ref]$r)
    $w = $r.R - $r.L; $ht = $r.B - $r.T
    $bmp = New-Object System.Drawing.Bitmap $w, $ht
    $g = [System.Drawing.Graphics]::FromImage($bmp); $g.CopyFromScreen($r.L, $r.T, 0, 0, $bmp.Size)
    $bmp.Save($file, [System.Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose()
    return "${w}x${ht} saved"
}

$script:results = @()
function Check($label, $ok, $detail) {
    $script:results += [pscustomobject]@{ Ok = [bool]$ok; Label = $label; Detail = $detail }
    "{0}  {1}   [{2}]" -f $(if ($ok) { 'PASS' } else { 'FAIL' }), $label, $detail
}

function Run-Suite {
    $t0 = Get-Date
    while ([K]::Fg() -ne 'SWTEST-A' -and ((Get-Date) - $t0).TotalSeconds -lt 6) { Start-Sleep -Milliseconds 200 }
    if ([K]::Fg() -ne 'SWTEST-A') { throw "ABORT: start state is '$([K]::Fg())', need SWTEST-A - no key sent" }
    "start: SWTEST-A in front, stack A,B,C"

    # 1. Hold Alt: the popup appears, the selection moves, nothing switches until Alt is released.
    try {
        Down $ALT 0x38; Guard
        Tap $TICK 0x29; Start-Sleep -Milliseconds 700
        $popupUp = PopupUp; $fgDuring1 = [K]::Fg(); $shot1 = ShotPopup (Join-Path $OutDir 'popup1.png')
        Tap $TICK 0x29; Start-Sleep -Milliseconds 300
        $shot2 = ShotPopup (Join-Path $OutDir 'popup2.png'); $fgDuring2 = [K]::Fg()
    } finally { Up $ALT 0x38; Start-Sleep -Milliseconds 500 }
    Check 'popup appears after holding Alt' $popupUp "visible=$popupUp, $shot1"
    Check 'nothing switches while Alt is held' ($fgDuring1 -eq 'SWTEST-A' -and $fgDuring2 -eq 'SWTEST-A') "in front during hold: $fgDuring1 / $fgDuring2"
    Check 'releasing Alt switches to the 2nd press (C)' ([K]::Fg() -eq 'SWTEST-C') "in front now: $([K]::Fg())"
    Check 'popup is gone after release' (-not (PopupUp)) "visible=$(PopupUp)"

    # 2. Quick tap: instant switch, no popup.
    Guard
    Down $ALT 0x38; Tap $TICK 0x29; Up $ALT 0x38; Start-Sleep -Milliseconds 600
    Check 'quick tap switches immediately (C -> A)' ([K]::Fg() -eq 'SWTEST-A' -and -not (PopupUp)) "in front now: $([K]::Fg()), popup visible=$(PopupUp)"

    # 3. Alt+Shift+` goes backwards. Stack is now A,C,B: one step back from A reaches the least recent (B).
    Guard
    try {
        Down $ALT 0x38; Down 0x10 0x2A; Tap $TICK 0x29; Start-Sleep -Milliseconds 300
    } finally { Up 0x10 0x2A; Up $ALT 0x38; Start-Sleep -Milliseconds 500 }
    Check 'Alt+Shift+` goes backwards (A -> B)' ([K]::Fg() -eq 'SWTEST-B') "in front now: $([K]::Fg())"

    # 4. Esc cancels. Stack is now B,A,C.
    Guard
    try {
        Down $ALT 0x38; Tap $TICK 0x29; Start-Sleep -Milliseconds 600
        $upBeforeEsc = PopupUp
        Tap $ESC 0x01; Start-Sleep -Milliseconds 300
        $goneAfterEsc = -not (PopupUp)
    } finally { Up $ALT 0x38; Start-Sleep -Milliseconds 400 }
    Check 'Esc closes the popup without switching' ($upBeforeEsc -and $goneAfterEsc -and [K]::Fg() -eq 'SWTEST-B') "popup was up=$upBeforeEsc, gone=$goneAfterEsc, in front: $([K]::Fg())"

    # 5. A minimised window shows up (with a preview) and is restored when picked. Stack is B,A,C: minimise C.
    Guard
    $c = Find 'SWTEST-C'; [void][K]::ShowWindow($c, 6); Start-Sleep -Milliseconds 500
    $wasMin = [K]::IsIconic($c)
    Guard
    try {
        Down $ALT 0x38; Tap $TICK 0x29; Start-Sleep -Milliseconds 100; Tap $TICK 0x29; Start-Sleep -Milliseconds 600
        $shot3 = ShotPopup (Join-Path $OutDir 'popup3.png')
    } finally { Up $ALT 0x38; Start-Sleep -Milliseconds 700 }
    Check 'minimised window is picked, restored and focused' ($wasMin -and [K]::Fg() -eq 'SWTEST-C' -and -not [K]::IsIconic($c)) "was minimised=$wasMin, in front: $([K]::Fg()), still minimised=$([K]::IsIconic($c)), $shot3"
}

# ---- setup ----------------------------------------------------------------
foreach ($exe in $AppExe, $TestWindowsExe) { if (-not (Test-Path $exe)) { throw "not found: $exe" } }
New-Item -ItemType Directory -Force $OutDir | Out-Null

$other = Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'AutoHotkey64.exe' -and $_.CommandLine -match 'SameAppSwitcher' }
if ($other) { throw "Another Alt+` switcher (AutoHotkey SameAppSwitcher) is running and would double-handle the keys. Exit it first." }

$appName = [IO.Path]::GetFileNameWithoutExtension($AppExe)
Get-Process $appName -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Resolve-Path $AppExe).Path } | Stop-Process -Force
$app = Start-Process $AppExe -PassThru
Start-Sleep -Milliseconds 1500
if ($app.HasExited) { throw "the app exited immediately (exit code $($app.ExitCode))" }
"app running (pid $($app.Id))"

try {
    for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
        $script:results = @()
        $tw = Start-Process $TestWindowsExe -PassThru
        Start-Sleep -Seconds 2
        $aborted = $false
        try { Run-Suite } catch { if ($_.Exception.Message -like 'ABORT*') { $aborted = $true; "($($_.Exception.Message))" } else { throw } }
        finally { Stop-Process -Id $tw.Id -Force -ErrorAction SilentlyContinue; Start-Sleep -Milliseconds 600 }
        if (-not $aborted) { break }
        "attempt $attempt aborted before/while sending keys; retrying with fresh windows..."
    }
}
finally {
    Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue
}

$failed = @($script:results | Where-Object { -not $_.Ok })
""
"{0} of {1} checks passed.   Popup screenshots: {2}" -f ($script:results.Count - $failed.Count), $script:results.Count, $OutDir
if ($failed.Count -gt 0 -or $script:results.Count -eq 0) { exit 1 }
