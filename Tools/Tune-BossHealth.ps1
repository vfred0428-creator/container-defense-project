# Finds the boss health whose median bot-sweep match length hits the target. Changes nothing unless -Apply.
# Only MatchRules.BossHealth is tuned (there is no separate health scaling); everything else stays default.
# Known points (30 seeds): 6500 -> 61 s, 40000 -> 176 s, 77500 -> 302 s, 120000 -> 446 s, 200000 -> 718 s.
param([switch]$Apply)
$TargetMatchSeconds = 300   # the one number to change: target median match length in seconds
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$work = Join-Path $root 'TestResults\Playtest\hp-tuning'
New-Item -ItemType Directory -Path $work -Force | Out-Null
function Median([float]$hp,[int]$seeds) {
  $dir = Join-Path $work ("hp-{0}-{1}" -f [int]$hp,$seeds)
  for ($try = 0; $try -lt 3; $try++) {
    try { & (Join-Path $PSScriptRoot 'Run-PlaytestSweep.ps1') -Seeds $seeds -BossHealth $hp -Out $dir | Out-Null; break } catch { Start-Sleep 5 }
  }
  $line = Get-Content (Join-Path $dir 'summary.txt') | Where-Object { $_ -match 'median (\d+)' } | Select-Object -First 1
  if ($line -notmatch 'median (\d+)') { throw "No median for $hp" }
  "  boss health {0,7}: median {1} s ({2} seeds)" -f [int]$hp,$Matches[1],$seeds | Write-Host
  return [int]$Matches[1]
}
# Match length grows roughly linearly with boss health, so bracket, then bisect to within 5 s.
$lo = 6500; $hi = 300000
for ($i = 0; $i -lt 12; $i++) {
  $mid = [math]::Round(($lo + $hi) / 2 / 500) * 500
  $m = Median $mid 30
  if ([math]::Abs($m - $TargetMatchSeconds) -le 5) { break }
  if ($m -lt $TargetMatchSeconds) { $lo = $mid } else { $hi = $mid }
}
$final = Median $mid 70
"Boss health $mid gives a median of $final s over 70 seeds (target $TargetMatchSeconds s)." | Tee-Object -FilePath (Join-Path $work 'result.txt')
if ($Apply) {
  $rules = Join-Path $root 'Assets\Scripts\Domain\MatchRules.cs'
  [IO.File]::WriteAllText($rules,((Get-Content $rules -Raw) -replace 'public float BossHealth = [0-9.]+;',"public float BossHealth = $mid;"))
  "Applied BossHealth = $mid to MatchRules.cs."
}
