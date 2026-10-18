<#
.SYNOPSIS
  Builds the app and packs the per-user installer: <Destination>\MohiAltSwitch-<version>-win-x64.zip
  plus an unzipped folder with Install.cmd ready to double-click, and SHA256 checksums.
#>
[CmdletBinding()]
param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$WorkRoot = (Join-Path $env:LOCALAPPDATA 'MohiAltSwitchBuild'),
    [string]$Destination = (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'MohiAltSwitch'),
    [switch]$SelfContained
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$buildArgs = @{ RepoRoot = $RepoRoot; WorkRoot = $WorkRoot }
if ($SelfContained) { $buildArgs.SelfContained = $true }
& (Join-Path $PSScriptRoot 'build.ps1') @buildArgs
if (-not $?) { throw 'build failed' }

[xml]$proj = Get-Content (Join-Path $RepoRoot 'src\MohiAltSwitch\MohiAltSwitch.csproj')
$version = ($proj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1)
$name = "MohiAltSwitch-$version-win-x64"

$stage = Join-Path $WorkRoot "stage\$name"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force (Join-Path $stage 'app') | Out-Null
Copy-Item (Join-Path $WorkRoot 'out\app\*') (Join-Path $stage 'app') -Recurse
foreach ($f in 'Install.cmd', 'install.ps1', 'uninstall.ps1', 'README.txt') {
    Copy-Item (Join-Path $RepoRoot "tools\installer\$f") $stage
}

New-Item -ItemType Directory -Force $Destination | Out-Null
$zip = Join-Path $Destination "$name.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
[IO.Compression.ZipFile]::CreateFromDirectory($stage, $zip, [IO.Compression.CompressionLevel]::Optimal, $true)

$unzipped = Join-Path $Destination $name
if (Test-Path $unzipped) { Remove-Item $unzipped -Recurse -Force }
Copy-Item $stage $unzipped -Recurse

$hash = (Get-FileHash $zip -Algorithm SHA256).Hash
"$hash  $name.zip" | Set-Content (Join-Path $Destination "$name.sha256.txt") -Encoding ascii

""
"zip:       $zip  ($([math]::Round((Get-Item $zip).Length / 1KB)) KB)"
"folder:    $unzipped   (double-click Install.cmd)"
"sha256:    $hash"
