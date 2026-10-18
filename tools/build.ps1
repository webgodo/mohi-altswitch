<#
.SYNOPSIS
  Builds the app (and the test helper) from the repo. Windows PowerShell 5.1 or later, .NET SDK 10.

.DESCRIPTION
  Windows build tools handle \\wsl.localhost paths badly, so the sources are first mirrored to a
  Windows-local work folder and built there. Outputs land in <WorkRoot>\out.
#>
[CmdletBinding()]
param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$WorkRoot = (Join-Path $env:LOCALAPPDATA 'MohiAltSwitchBuild'),
    [string]$Configuration = 'Release',
    [switch]$SelfContained            # needs the .NET runtime pack from nuget.org (a one-time download)
)
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'; $env:DOTNET_NOLOGO = '1'
$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) { $dotnet = 'C:\Program Files\dotnet\dotnet.exe' }

$work = Join-Path $WorkRoot 'work'
$out  = Join-Path $WorkRoot 'out'
New-Item -ItemType Directory -Force $work, $out | Out-Null

"== mirror sources -> $work"
robocopy $RepoRoot $work /MIR /XD .git bin obj dist .vs /XF *.user /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE)" }

$app   = Join-Path $work 'src\MohiAltSwitch\MohiAltSwitch.csproj'
$tests = Join-Path $work 'tests\TestWindows\TestWindows.csproj'

"== build ($Configuration)"
& $dotnet build $app   -c $Configuration --nologo -v minimal -warnaserror
if ($LASTEXITCODE) { throw "app build failed" }
& $dotnet build $tests -c $Configuration --nologo -v minimal
if ($LASTEXITCODE) { throw "test helper build failed" }

"== publish app"
$publishDir = Join-Path $out 'app'
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
$publishArgs = @('publish', $app, '-c', $Configuration, '-r', 'win-x64', '-o', $publishDir, '--nologo', '-v', 'minimal')
if ($SelfContained) {
    $publishArgs += @('--self-contained', 'true', '-p:PublishSingleFile=true', '-p:EnableCompressionInSingleFile=true',
                      '-p:PublishTrimmed=true', '-p:TrimMode=link', '-p:DebugType=none')
} else {
    $publishArgs += @('--self-contained', 'false', '-p:DebugType=none')
}
& $dotnet @publishArgs
if ($LASTEXITCODE) { throw "publish failed" }

$testOut = Join-Path $out 'testwindows'
if (Test-Path $testOut) { Remove-Item $testOut -Recurse -Force }
& $dotnet publish $tests -c $Configuration -r win-x64 --self-contained false -o $testOut --nologo -v minimal
if ($LASTEXITCODE) { throw "test helper publish failed" }

""
"app:           $publishDir\MohiAltSwitch.exe"
"test windows:  $testOut\TestWindows.exe"
Get-ChildItem $publishDir | Select-Object Name, @{n='KB';e={[int]($_.Length / 1KB)}} | Format-Table -AutoSize
