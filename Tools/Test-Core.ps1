$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskOutput = Join-Path $taskRoot 'TestResults'
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $taskCompiler)) { throw 'The Windows .NET Framework C# compiler is required.' }
$taskSources = @(Get-ChildItem (Join-Path $taskRoot 'Assets\Scripts\Domain') -Filter '*.cs' | ForEach-Object FullName)
$taskSources += Join-Path $taskRoot 'Assets\Tests\Editor\CoreTests.cs'
$taskSources += Join-Path $taskRoot 'Assets\Tests\Editor\MilestoneTwoTests.cs'
$taskSources += Join-Path $taskRoot 'Assets\Tests\Editor\CollectionTests.cs'
$taskSources += Join-Path $taskRoot 'Assets\Tests\Editor\SocialTests.cs'
$taskSources += Join-Path $taskRoot 'Assets\Tests\Editor\RankingTests.cs'
$taskSources += Join-Path $taskRoot 'Assets\Tests\Editor\NeighborhoodTests.cs'
$taskSources += Join-Path $taskRoot 'Assets\Tests\Editor\MatchContractTests.cs'
$taskSources += Join-Path $taskRoot 'Assets\Tests\Editor\MatchViewTests.cs'
$taskExe = Join-Path $taskOutput 'CoreTests.exe'
& $taskCompiler /nologo /warnaserror+ /optimize+ /target:exe "/out:$taskExe" $taskSources
if ($LASTEXITCODE -ne 0) { throw 'Core compilation failed.' }
& $taskExe | Tee-Object -FilePath (Join-Path $taskOutput 'core-results.txt')
if ($LASTEXITCODE -ne 0) { throw 'Core regression tests failed.' }

