# Claude latest handoff — 2026-10-01

## Build checkpoint
- Branch claude/continue-astra, local only.
- Preview player (development build): TestResults\UI-Pass\preview-build\ContainerDefense.exe, rebuilt after the boss HP commit.
- Builds\Windows\ContainerDefense.exe is untouched.
- Tests: Tools/Test-Core.ps1 (domain, incl. 7 board, 10 match view, 30 match contract) plus 17 Unity persistence checks all pass. HUD audit 0 at 1920x1080, 1280x720, 1920x886, 1920x864, 1600x720 and 1560x720.
- APK: not made. Android Build Support is not installed, per vhi's instruction.

## Since the last handoff
- **a16a072 Board rules:** YardLayout (12x7 grid, 8 pads), WeaponPlacement.Spot, PlayerCommands.Place(slot,kind,spot)/MoveToSpot, AccountData.Yard, and asleep = indoors (Position = Entry).
- **f97ebf4 Board view:**
  - BoardView.cs (ArenaView partial) is a stage at (1000,0,0) showing the viewed house.
  - YardEditorHud.cs is the MY YARD screen.
  - The pad placing flow is in HouseBoardHud.
  - MatchView.AllowScouting is the scouting flag.
- **b36d5b0 Placement:** boss-kill survivors rank by damage, then HP, then claim time (PlayerState.ClaimedAt). Rank and wins use placement 1.
- **2b5fefc Boss HP:** 77,500 for a ~5 min median (TestResults/Playtest/boss-hp-tuning.md, Tools/Tune-BossHealth.ps1).
- No old camera tests asserted the old framing, so none changed.

## Open
- Music: 5b1a497 adds a CC0 loop (MintoDog), 40% of the Music slider, ducked for warnings and jingles.
- Real touch-device QA.
