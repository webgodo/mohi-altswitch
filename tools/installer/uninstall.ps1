<#
.SYNOPSIS
  Removes Mohi AltSwitch for the current user: the app, shortcut, start-up entry and Settings > Apps entry.
  Pass -KeepSettings to leave your preferences (%LOCALAPPDATA%\MohiAltSwitch) in place.
#>
[CmdletBinding()]
param([switch]$KeepSettings)
$ErrorActionPreference = 'Continue'

$target  = Join-Path $env:LOCALAPPDATA 'Programs\Mohi AltSwitch'
$runKey  = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\MohiAltSwitch'
$shortcut = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Mohi AltSwitch.lnk'

Get-Process 'MohiAltSwitch' -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -and $_.Path.StartsWith($target, [StringComparison]::OrdinalIgnoreCase) } |
    ForEach-Object { $_.Kill(); $_.WaitForExit(5000) | Out-Null }

Remove-ItemProperty $runKey -Name 'MohiAltSwitch' -ErrorAction SilentlyContinue
Remove-Item $shortcut -ErrorAction SilentlyContinue
Remove-Item $uninstallKey -Recurse -ErrorAction SilentlyContinue
if (-not $KeepSettings) { Remove-Item (Join-Path $env:LOCALAPPDATA 'MohiAltSwitch') -Recurse -Force -ErrorAction SilentlyContinue }

# This script lives inside the folder it is deleting, so hand the final delete to a helper that waits for us to exit.
$cmd = "ping -n 3 127.0.0.1 >nul & rmdir /s /q `"$target`""
Start-Process cmd.exe -ArgumentList '/c', $cmd -WindowStyle Hidden

"Mohi AltSwitch has been removed."
