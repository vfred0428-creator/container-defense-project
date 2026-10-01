# Headless playtest sweep over the real domain simulation (no Unity). Output: TestResults/Playtest.
# -BossHealth overrides MatchRules.BossHealth for tuning runs; -Out writes the summary somewhere else.
param([int]$Seeds = 70, [float]$BossHealth = 0, [string]$Out = "")
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$bin = Join-Path $root 'TestResults\Playtest'
$out = if ($Out) { $Out } else { $bin }
New-Item -ItemType Directory -Path $bin,$out -Force | Out-Null
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources = @(Get-ChildItem (Join-Path $root 'Assets\Scripts\Domain') -Filter '*.cs' | ForEach-Object FullName) + (Join-Path $PSScriptRoot 'PlaytestSweep.cs')
$exe = Join-Path $bin 'PlaytestSweep.exe'
& $csc /nologo /optimize+ /target:exe "/out:$exe" $sources
if ($LASTEXITCODE -ne 0) { throw 'Sweep compilation failed.' }
if ($BossHealth -gt 0) { & $exe $Seeds $out $BossHealth } else { & $exe $Seeds $out }
