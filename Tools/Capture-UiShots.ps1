# Captures UI screenshots from a development player. Default: the screenshot-only preview build
# (TestResults\UI-Pass\preview-build), driven by --ui-shots with an isolated account. Builds nothing.
# -Legacy uses the older smoke drivers, which is how the before shots were taken from Builds\Windows.
param([string]$Player = (Join-Path (Split-Path $PSScriptRoot -Parent) 'TestResults\UI-Pass\preview-build\ContainerDefense.exe'),
      [string]$Output = (Join-Path (Split-Path $PSScriptRoot -Parent) 'TestResults\UI-Pass\after'),
      [switch]$Legacy)
$ErrorActionPreference = 'Stop'
if ($Legacy) {
  # 1920x886 is the 2340x1080 (19.5:9) aspect scaled to fit a 1920-wide desktop.
  $sizes = @(@(1920,1080,0),@(1280,720,0),@(1920,886,0))
  $runs = @(@('match','--ranked-smoke-test'),@('collection','--collection-smoke-test'),@('social','--social-smoke-test'))
  foreach ($s in $sizes) { foreach ($r in $runs) {
    $dir = Join-Path $Output ("{0}x{1}-{2}" -f $s[0],$s[1],$r[0]); if (Test-Path $dir) { Remove-Item -Recurse -Force $dir }; New-Item -ItemType Directory -Force $dir | Out-Null
    $p = Start-Process -FilePath $Player -ArgumentList "-screen-fullscreen 0 -screen-width $($s[0]) -screen-height $($s[1]) --smoke-test $($r[1]) --smoke-output `"$dir`" -logFile `"$dir\player.log`"" -PassThru
    if (-not $p.WaitForExit(600000)) { $p.Kill(); "$dir timed out" } else { "$dir exit=$($p.ExitCode)" } } }
  return
}
# Wide phone: 2340x1080 does not fit a 1920 desktop, so 1920x886 keeps its 19.5:9 aspect with a scaled 108 px notch each side.
$sizes = @(@(1920,1080,0),@(1280,720,0),@(1920,886,108))
foreach ($s in $sizes) {
  $dir = Join-Path $Output ("{0}x{1}" -f $s[0],$s[1]); if (Test-Path $dir) { Remove-Item -Recurse -Force $dir }; New-Item -ItemType Directory -Force $dir | Out-Null
  $account = Join-Path $dir 'account'
  $p = Start-Process -FilePath $Player -ArgumentList "-screen-fullscreen 0 -popupwindow -screen-width $($s[0]) -screen-height $($s[1]) --smoke-test --manual-test --smoke-output `"$account`" --ui-shots `"$dir`" --notch $($s[2]) -logFile `"$dir\player.log`"" -PassThru
  if (-not $p.WaitForExit(300000)) { $p.Kill(); "$dir timed out" } else { "$dir exit=$($p.ExitCode)" }
  Get-Content (Join-Path $dir 'result.txt') -ErrorAction SilentlyContinue
}
