# Claude latest handoff

- Branch: claude/continue-astra (local only, not pushed). Commit: see `git log -1` after this file's commit (step 2, TFT camera).
- Screenshot/preview player: C:\Users\vfred\OneDrive\Documents\ChatGPT\game\TestResults\UI-Pass\preview-build\ContainerDefense.exe (development build)
- Known-good player left untouched: C:\Users\vfred\OneDrive\Documents\ChatGPT\game\Builds\Windows\ContainerDefense.exe (built at f71d2ce, before last-one-wins and the art passes)
- Tests: Tools/Test-Core.ps1 = 133 domain checks (19 core, 16 progression, 16 collection, 16 social, 12 ranking, 18 neighborhood, 27 match contract, 9 match view) + 17 Unity persistence = 150 pass. No known failures. HUD audit: 0 warnings at 1920x1080, 1280x720, 1920x886 (wide-phone aspect, simulated notch).
- Changed since last handoff:
  - Art pass 2 (fd378ab): mock 3x4 layout and rabbit plaza, generated houses, bodies, portraits, asphalt, dock, edge stacks, props, boss and minions, warm grade.
  - TFT camera: Domain/MatchView.cs (neighbourhood -> own base -> scout living players read-only -> full map). Runtime wiring is in GameSession, MatchHud (full-map overlay, banner, threat edge, GO HOME, minimap tap) and ArenaView.HouseScreenRect. Tests: Tests/Editor/MatchViewTests.cs.
- Screens: TestResults/UI-Pass/after/<size>/*.png; comparisons with the mocks are in TestResults/UI-Pass/vs-mocks.
- Ownership: Codex owns README.md, PROJECT_STATUS.md and Tools/Verify-Player.ps1; Claude does not edit them.
