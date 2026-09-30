# Captures UI screenshots from an already-built player at several window sizes, using the
# existing smoke drivers (isolated accounts). Does not build anything.
param([string]$Player = (Join-Path (Split-Path $PSScriptRoot -Parent) 'Builds\Windows\ContainerDefense.exe'),
      [string]$Output = (Join-Path (Split-Path $PSScriptRoot -Parent) 'TestResults\UI-Pass\before'))
$ErrorActionPreference = 'Stop'
# 1920x886 is the 2340x1080 (19.5:9) aspect scaled to fit a 1920-wide desktop.
$sizes = @(@(1920,1080),@(1280,720),@(1920,886))
$runs = @(@('match','--ranked-smoke-test'),@('collection','--collection-smoke-test'),@('social','--social-smoke-test'))
foreach ($s in $sizes) {
  foreach ($r in $runs) {
    $dir = Join-Path $Output ("{0}x{1}-{2}" -f $s[0],$s[1],$r[0])
    if (Test-Path $dir) { Remove-Item -Recurse -Force $dir }
    New-Item -ItemType Directory -Force $dir | Out-Null
    $arguments = "-screen-fullscreen 0 -screen-width $($s[0]) -screen-height $($s[1]) --smoke-test $($r[1]) --smoke-output `"$dir`" -logFile `"$dir\player.log`""
    $p = Start-Process -FilePath $Player -ArgumentList $arguments -PassThru
    if (-not $p.WaitForExit(600000)) { $p.Kill(); Write-Output "$dir timed out" } else { Write-Output "$dir exit=$($p.ExitCode)" }
  }
}
