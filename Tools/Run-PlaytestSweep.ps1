# Headless playtest sweep over the real domain simulation (no Unity). Output: TestResults/Playtest.
param([int]$Seeds = 70)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'TestResults\Playtest'
New-Item -ItemType Directory -Path $out -Force | Out-Null
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources = @(Get-ChildItem (Join-Path $root 'Assets\Scripts\Domain') -Filter '*.cs' | ForEach-Object FullName) + (Join-Path $PSScriptRoot 'PlaytestSweep.cs')
$exe = Join-Path $out 'PlaytestSweep.exe'
& $csc /nologo /optimize+ /target:exe "/out:$exe" $sources
if ($LASTEXITCODE -ne 0) { throw 'Sweep compilation failed.' }
& $exe $Seeds $out
