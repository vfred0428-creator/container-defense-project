# Claude latest handoff — 2026-10-01

## Build checkpoint
- Branch claude/continue-astra (local, not pushed). Source commit: 4b67a36 (this handoff file is committed right after it).
- Built player (development, screenshot driver included): C:\Users\vfred\OneDrive\Documents\ChatGPT\game\TestResults\UI-Pass\preview-build\ContainerDefense.exe
  - ContainerDefense.Runtime.dll SHA256 10328829E14C1E0300464CE6C869DB4EC30B53329BA191178806870CDA201692
- Known-good player left untouched: Builds\Windows\ContainerDefense.exe (built at f71d2ce).
- Tests: Tools/Test-Core.ps1 = 133 domain checks (19 core, 16 progression, 16 collection, 16 social, 12 ranking, 18 neighborhood, 27 match contract, 9 match view) + 17 Unity persistence = 150 pass. No known failures.
- HUD audit: 0 warnings at 1920x1080, 1280x720 and 1920x886 (wide-phone aspect with a simulated 108 px notch).
- Screens: TestResults/UI-Pass/after/<size>/01..18.png. Game-vs-mock comparisons: TestResults/UI-Pass/vs-mocks/*.jpg.

## Done since the last handoff
- fd378ab Art pass 2: mock 3x4 layout and rabbit plaza, generated containers, bodies, portraits, asphalt, dock, edge stacks, props, boss and minions, warm grade.
- f2455fd TFT camera (Domain/MatchView.cs, Tests/Editor/MatchViewTests.cs):
  - neighbourhood during the claim race, then your own base
  - read-only scouting of living players
  - full map (minimap or FULL MAP)
  - home-threat edge with GO HOME
- fa812a0 Feedback: hit sparks, screen shake on your door, coin pops, boss hit flash, round portrait frames.
- db40987 House panel thumbnail and big number, larger weapon art, round minimap.
- 4b67a36 Skull icon on the boss bar, house markers on the minimap.

## Generated art on hand
- In-game copies: Assets/Resources/Art2D/Generated.
- Originals:
  - Art/Source/Generated (v1 tiles and icons)
  - Art/Source/Generated/v2 (houses, bodies, portraits, boss, minion, weapons, props, plaza, asphalt, dock and edge strips, menu background, logo)
  - Art/Source/Generated/v3 (icon_bed, icon_door, icon_repair, icon_skull, icon_house, icon_up)
- Not yet wired, left for the button rework: icon_bed, icon_door, icon_repair, icon_up.
- References: Art/References. These are vhi's 4 mocks plus the two 12-house top-down mocks.

## Offer to Codex/Astra (vhi asked us to assist your buttons, mechanics and background-art rework)
I can take any of these:
- **New art:** generate it with Higgsfield to your exact spec. About 100 credits are left. I'll download it, crop and resize it, and import it.
- **Wiring:** hook art into sprites and HUD textures.
- **Unity:** run batchmode checks and preview builds. I'm the only Unity runner.
- **Screenshots:** capture 18 screens at three sizes with Tools/Capture-UiShots.ps1 and compare them side by side with the mocks.
- **Tests:** run them and report results.

Please write the piece you want me to take into Tools/Codex-Requests-For-Claude.md: file names, sizes, style, which screen, and the files you are editing. I'll stay out of your files, stage only by explicit path, and report back in that file.

## Ownership
- Codex: README.md, PROJECT_STATUS.md, Tools/Verify-Player.ps1, and the buttons, mechanics and background-art rework.
- Claude: camera, ArenaView, GameSession, MatchView and its tests, and art import. HUD files only where Codex has not claimed them.
