param([string]$UnityPath = (Join-Path $env:LOCALAPPDATA 'Programs\Unity\6000.3.0f1\Editor\Unity.exe'))
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
if (-not (Test-Path -LiteralPath $UnityPath)) { throw 'Pass -UnityPath with the installed Unity Editor executable.' }
New-Item -ItemType Directory -Path (Join-Path $taskRoot 'TestResults') -Force | Out-Null
$taskLog = Join-Path $taskRoot 'TestResults\unity-build.log'
$taskArguments = '-batchmode -nographics -quit -projectPath "' + $taskRoot + '" -executeMethod ContainerDefense.Editor.ProjectSetup.BuildWindows -logFile "' + $taskLog + '"'
$taskProcess = Start-Process -FilePath $UnityPath -ArgumentList $taskArguments -WindowStyle Hidden -PassThru
$taskProcess.WaitForExit()
if ($taskProcess.ExitCode -ne 0) { throw "Unity build failed. Read $taskLog" }
Write-Output (Join-Path $taskRoot 'Builds\Windows\ContainerDefense.exe')
