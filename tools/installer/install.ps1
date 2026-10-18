<#
.SYNOPSIS
  Per-user installer for Mohi AltSwitch. No administrator rights needed.

.DESCRIPTION
  Copies the app to %LOCALAPPDATA%\Programs\Mohi AltSwitch, adds a Start menu shortcut, registers an entry in
  Settings > Apps (so it can be uninstalled normally) and, unless -NoAutostart is given, starts it at sign-in.
#>
[CmdletBinding()]
param(
    [switch]$NoAutostart,
    [switch]$NoLaunch
)
$ErrorActionPreference = 'Stop'

$source  = Join-Path $PSScriptRoot 'app'
$target  = Join-Path $env:LOCALAPPDATA 'Programs\Mohi AltSwitch'
$exeName = 'MohiAltSwitch.exe'
$runKey  = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\MohiAltSwitch'
$shortcut = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Mohi AltSwitch.lnk'

if (-not (Test-Path (Join-Path $source $exeName))) { throw "Can't find $exeName next to this script (expected in '$source')." }

# A framework-dependent build needs the .NET 10 runtime; a self-contained one has no runtimeconfig.json and needs nothing.
if (Test-Path (Join-Path $source 'MohiAltSwitch.runtimeconfig.json')) {
    $dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
    if (-not $dotnet -and (Test-Path 'C:\Program Files\dotnet\dotnet.exe')) { $dotnet = 'C:\Program Files\dotnet\dotnet.exe' }
    $hasRuntime = $dotnet -and (& $dotnet --list-runtimes 2>$null | Where-Object { $_ -like 'Microsoft.NETCore.App 10.*' })
    if (-not $hasRuntime) {
        throw ".NET 10 runtime not found. Install it first:  winget install Microsoft.DotNet.Runtime.10"
    }
}

# Stop a running copy so its files can be replaced.
Get-Process ([IO.Path]::GetFileNameWithoutExtension($exeName)) -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -and $_.Path.StartsWith($target, [StringComparison]::OrdinalIgnoreCase) } |
    ForEach-Object { $_.Kill(); $_.WaitForExit(5000) | Out-Null }

New-Item -ItemType Directory -Force $target | Out-Null
Copy-Item (Join-Path $source '*') $target -Recurse -Force
Copy-Item (Join-Path $PSScriptRoot 'uninstall.ps1') $target -Force

$exe = Join-Path $target $exeName
$version = (Get-Item $exe).VersionInfo.ProductVersion
if (-not $version) { $version = '0.0.0' }

# Start menu shortcut
$sh = New-Object -ComObject WScript.Shell
$lnk = $sh.CreateShortcut($shortcut)
$lnk.TargetPath = $exe
$lnk.WorkingDirectory = $target
$lnk.Description = 'Alt+` switches between windows of the same app'
$lnk.Save()

# Entry in Settings > Apps > Installed apps
New-Item -Force $uninstallKey | Out-Null
$sizeKb = [int]((Get-ChildItem $target -Recurse -File | Measure-Object Length -Sum).Sum / 1KB)
$entries = @{
    DisplayName     = 'Mohi AltSwitch'
    DisplayVersion  = $version
    Publisher       = 'Mohi'
    InstallLocation = $target
    DisplayIcon     = $exe
    UninstallString = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$(Join-Path $target 'uninstall.ps1')`""
    URLInfoAbout    = 'https://github.com/webgodo/mohi-altswitch'
}
foreach ($k in $entries.Keys) { Set-ItemProperty $uninstallKey -Name $k -Value $entries[$k] }
Set-ItemProperty $uninstallKey -Name NoModify -Value 1 -Type DWord
Set-ItemProperty $uninstallKey -Name NoRepair -Value 1 -Type DWord
Set-ItemProperty $uninstallKey -Name EstimatedSize -Value $sizeKb -Type DWord

if (-not $NoAutostart) { Set-ItemProperty $runKey -Name 'MohiAltSwitch' -Value "`"$exe`"" }

if (-not $NoLaunch) { Start-Process $exe }

""
"Mohi AltSwitch $version installed to:"
"  $target"
'Press Alt + ` to switch between windows of the same app. Hold Alt for the preview strip.'
"Settings are in the tray icon's menu. Uninstall any time from Settings > Apps."
'If AltExten (or another Alt+` tool) is running, set its window hotkey to something else, or one of them will win.'
