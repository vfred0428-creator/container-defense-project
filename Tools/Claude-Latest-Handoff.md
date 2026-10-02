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

## Open (see PROJECT_STATUS "Claude, while Astra was out")
- Music: 5b1a497 adds a CC0 loop (MintoDog), 40% of the Music slider, ducked for warnings and jingles.
- Real touch-device QA.

## Codex continuation — 2026-10-01
After Claude usage stopped, Codex resumed the unfinished room runtime edits following 32c3972. See PROJECT_STATUS.md for the current concise handoff, final build and test evidence. Room station controls, queue safety, scaled transforms and modal input blocking are now integrated. The shipped DefaultMatch.asset was still at 6,500 boss HP; it now matches the committed 77,500 tuning, with a build guard. Launch/loading and further character-selection polish remain next.

## Claude — 2026-10-01 afternoon
Continued on Astra's b8cb807. Done: room card polish (c31f928), launch screen with real loading/error/retry (d9b8694, 3a66029), smoother character selection (52e5c76). GitHub remote: origin = vfred0428-creator/container-defense-project (private). Pushes from this session are blocked by the auto-mode check; push `main` and `claude/continue-astra` from a terminal. Open: real phone QA, per-character rooms.

## Claude — 2026-10-02 (cloud session)
Per-character rooms, first pass (the open item above), on branch claude/amazing-cori-vg3ybx:
- Domain/RoomTheme.cs: each resident's room colours (accent, paper, ink) and Keepsake, which follows the passive (coin jar, shield, target, pillow, sneaker, price tag, wrench). Visual only. 1 new interior test, so 160 domain checks.
- HouseInteriorHud.RoomDecor covers the three painted bunny posters: a framed poster of the resident with their name, a keepsake card, and a swaying pennant with their initial. Same shared room.png; no new art.
- Checked here without Unity: domain + tests compile as C# 5 (like Test-Core.ps1's csc) under Mono and all 160 pass; Domain + Runtime compile with Roslyn against Unity 2021.3 reference assemblies. Placement was mocked on room.png.
- Not yet seen in Unity: please run Tools/Test-Core.ps1, Run Core Checks, Capture-UiShots and look at the room with a few characters before calling it done.

## Claude — 2026-10-02, backend bug hunt (same branch)
- **812e423 Ranked quit fix:** leaving a Practice Ranked match early (pause > Quit to title, Restart, closing or crashing the game) never recorded a result, so a losing player could quit to keep their stars. AccountData.RankedSequence (saved when a ranked match begins, no version bump) lets the account settle any unrewarded ranked match as one loss, no XP: on quit/restart, at the next match start, or on the next load. Restart in a ranked match stays ranked; the pause menu warns. RankingTests: one expectation updated to the new rule, two tests added (162 domain checks).
- Fuzzed without finding new bugs: 3,300 random six-player matches (random rules, rosters, valid and invalid commands, odd step sizes) checking gold, house HP, levels, pad spots, ownership, elimination, placements 1-6 and that every match ends; and 120,000 random gifts with a save store that fails 20% of the time, checking stickers are conserved, failed gifts change nothing and Popularity equals its receipts.
- Still to check in Unity: SaveTests (persistence) and the ranked smoke run with Verify-Player.ps1, since the account file gained a field.
