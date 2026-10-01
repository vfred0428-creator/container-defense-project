# Runs existing development-player checks without starting Unity or touching real accounts.
# A unique run directory and a copied player let another agent keep building independently.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Player,
    [ValidateSet('Match','Collection','Social')][string[]]$Suites = @('Match','Collection','Social'),
    [ValidateRange(640,3840)][int]$Width = 1280,
    [ValidateRange(480,2160)][int]$Height = 720,
    [ValidateRange(30,600)][int]$TimeoutSeconds = 240
)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskPlayer = (Resolve-Path -LiteralPath $Player).Path
$taskSource = Split-Path $taskPlayer -Parent
$taskName = [IO.Path]::GetFileNameWithoutExtension($taskPlayer)
$taskAssemblyRelative = $taskName + '_Data\Managed\ContainerDefense.Runtime.dll'
$taskAssembly = Join-Path $taskSource $taskAssemblyRelative
if (-not (Test-Path -LiteralPath $taskAssembly)) { throw 'Expected a Mono development player with ContainerDefense.Runtime.dll.' }
$taskBytes = [IO.File]::ReadAllBytes($taskAssembly)
$taskMetadata = [Text.Encoding]::UTF8.GetString($taskBytes)
$taskStrings = [Text.Encoding]::Unicode.GetString($taskBytes)
if (-not $taskMetadata.Contains('MatchSmokeDriver') -or -not $taskMetadata.Contains('SocialSmokeDriver') -or
    -not $taskMetadata.Contains('CollectionSmokeDriver') -or -not $taskStrings.Contains('--smoke-output')) {
    throw 'This player does not expose the isolated development smoke drivers. Refusing to launch against a real account.'
}
$taskRunName = 'Assist-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,6)
$taskRun = Join-Path (Join-Path $taskRoot 'TestResults') $taskRunName
$taskCopy = Join-Path $taskRun 'player'
New-Item -ItemType Directory -Path $taskCopy -Force | Out-Null
$taskBefore = (Get-FileHash -LiteralPath $taskAssembly -Algorithm SHA256).Hash
Get-ChildItem -LiteralPath $taskSource -Force | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $taskCopy -Recurse -Force }
$taskCopied = (Get-FileHash -LiteralPath (Join-Path $taskCopy $taskAssemblyRelative) -Algorithm SHA256).Hash
if ($taskCopied -ne $taskBefore -or (Get-FileHash -LiteralPath $taskAssembly -Algorithm SHA256).Hash -ne $taskBefore) {
    throw "The source player changed during copying. Retry when its build is stable. Incomplete snapshot retained at $taskRun"
}
$taskSourceHead = (& git -C $taskRoot rev-parse HEAD).Trim()
$taskManifest = [ordered]@{
    StartedUtc = [DateTime]::UtcNow.ToString('o'); SourcePlayer = $taskPlayer;
    SourceAssemblyWrittenUtc = (Get-Item -LiteralPath $taskAssembly).LastWriteTimeUtc.ToString('o');
    AssemblySha256 = $taskCopied; SourceHeadAtCopy = $taskSourceHead;
    Note = 'Source HEAD is context only; the assembly hash identifies the tested binary. Source may contain concurrent uncommitted work.';
    Width = $Width; Height = $Height; Suites = $Suites; Results = @()
}
$taskManifestPath = Join-Path $taskRun 'verification.json'
function Save-Manifest { $taskManifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $taskManifestPath -Encoding UTF8 }
Save-Manifest
Write-Output "Isolated verification: $taskRun"
$taskFlags = @{ Match = @('--ranked-smoke-test','--smoke-seed-account'); Collection = @('--collection-smoke-test'); Social = @('--social-smoke-test') }
$taskReload = @{ Match = @('--ranked-smoke-test','--smoke-reload-only'); Collection = @('--collection-smoke-test','--collection-reload-only'); Social = @('--social-smoke-test','--social-reload-only') }
foreach ($taskSuite in $Suites) {
    $taskAccount = Join-Path $taskRun $taskSuite
    New-Item -ItemType Directory -Path $taskAccount | Out-Null
    foreach ($taskIsReload in @($false,$true)) {
        $taskPhase = if ($taskIsReload) { 'reload' } else { 'initial' }
        $taskResultName = if ($taskIsReload) { 'reload-result.txt' } else { 'result.txt' }
        $taskResult = Join-Path $taskAccount $taskResultName
        $taskLog = Join-Path $taskAccount ($taskPhase + '-player.log')
        $taskOptions = if ($taskIsReload) { $taskReload[$taskSuite] } else { $taskFlags[$taskSuite] }
        $taskArguments = '-screen-fullscreen 0 -screen-width ' + $Width + ' -screen-height ' + $Height +
            ' --smoke-test ' + ($taskOptions -join ' ') + ' --smoke-output "' + $taskAccount + '" -logFile "' + $taskLog + '"'
        # Visible window is intentional: hidden players return black screenshots.
        $taskProcess = Start-Process -FilePath (Join-Path $taskCopy ([IO.Path]::GetFileName($taskPlayer))) -ArgumentList $taskArguments -PassThru
        $taskWatch = [Diagnostics.Stopwatch]::StartNew()
        while (-not $taskProcess.HasExited -and $taskWatch.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
            $null = $taskProcess.WaitForExit(1000)
        }
        $taskTimedOut = -not $taskProcess.HasExited
        if ($taskTimedOut) { $taskProcess.Kill(); $taskProcess.WaitForExit() }
        $taskExitCode = $taskProcess.ExitCode
        $taskText = if (Test-Path -LiteralPath $taskResult) { (Get-Content -LiteralPath $taskResult -Raw).Trim() } else { 'MISSING RESULT' }
        $taskPassed = -not $taskTimedOut -and $taskExitCode -eq 0 -and $taskText.StartsWith('PASS:')
        $taskManifest.Results += [ordered]@{ Suite = $taskSuite; Phase = $taskPhase; Passed = $taskPassed; ExitCode = $taskExitCode; TimedOut = $taskTimedOut; Seconds = [Math]::Round($taskWatch.Elapsed.TotalSeconds,1); Result = $taskText }
        Save-Manifest
        Write-Output "$taskSuite / $taskPhase : $taskText"
        if (-not $taskPassed) { throw "Player verification failed: $taskSuite / $taskPhase. Evidence: $taskAccount" }
    }
}
$taskManifest.CompletedUtc = [DateTime]::UtcNow.ToString('o')
Save-Manifest
Write-Output "PASS: all requested player checks and separate-process reloads. Evidence: $taskManifestPath"
