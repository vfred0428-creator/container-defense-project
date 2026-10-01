# Container Defense — project status

Updated 2026-09-30 (America/New_York). Continue this project; do not restart it. Twelve houses supersede the older six-house layout. One Default skin per each of the seven characters remains the catalog limit.

## WORKING
- Core: six solo participants (one human + five local bots), 12 stable house IDs, tap-to-walk/keyboard movement, exclusive claiming, personal sleeping income, gold, bed/door upgrades and repair.
- Visible defenses: three rooftop sockets per house; Gatling, Cannon, Slow and Rocket placement, timed construction, range-based firing, upgrades, movement preserving cooldown/investment, and 50% actual-investment sale refunds.
- Boss: seeded edge entry and route selection, fixed road graph, telegraph, bounded movement, maximum three hits per visit, 18-second target protection, least-visited targeting and skipping vacated/destroyed houses.
- Competitive endings: elimination, spectating, last standing after multiple claimants, boss defeat and all-fallen outcomes, deterministic placements and once-only rewards.
- M2: seven personal passives, permanent XP/account levels/unlocks at 1/3/5/7/9/12/15, selection and save migration/recovery.
- M3: seven active Default skins, five stackable Int64 sticker types, one-time starter grant, unique-collection Charisma, preserved historical ownership and persistence.
- M4 local prototype: profile, two explicitly local practice inboxes, multi-copy gifting, confirmation/retry identity, atomic debit/credit/Popularity/receipt, unread notifications and gift history. Rank, Charisma and Popularity are separate.
- M5 local prototype: eight solo rank tiers, five-star promotions, tier floors, highest rank, points/season/stats, Practice Ranked and three local leaderboard tables.
- Claude committed the domain contract hardening, last-standing outcome and latest art/HUD pass. Active generated art includes numbered container houses, rooftop weapons, portraits/bodies, boss/minions, dock, lighting and menu. Astra reviewed current source and tested the built preview independently.

## PARTIAL
- Claude currently owns the base-focused camera, full-map mode, read-only portrait scouting and off-screen home-threat warning. These are active edits; do not overwrite ArenaView, GameSession, MatchHud, HouseBoardHud, MatchView or their tests while that work is in progress. Await Tools/Claude-Latest-Handoff.md for the next build checkpoint.
- The visual direction remains a prototype. The copied preview's overview makes houses small and focused views still show adjacent houses; Claude's current camera task addresses this. Generated single-pose bodies use code-driven motion; the older nine-pose atlases remain available. Final cast consistency and animation need visual review against user references.
- UI remains IMGUI. Safe-area/notch layout exists and desktop tap/mouse input works, but Android builds, real touch-device QA and performance profiling are not validated.
- Local simulation only: no online match authority, cloud saves, real recipients, cross-device gifting or store integrations. Local receipt ledger is bounded to 1,000 and fails closed when full.
- Ranking still rewards a surviving boss-defeat result, not placement-based competitive scoring. Final balance and progression rules remain provisional.

## BROKEN
- No failures in the independently tested preview or current 124-scenario domain checkpoint below.
- Older TestResults/TopDown-Match/result.txt records a failed 03:30 build; it is stale evidence, not the latest result.
- In-progress camera edits have not yet received this independent player run. Do not equate a source change or screenshot with a completed build verification.

## OBSOLETE
- Old 3D character factories and legacy art are preserved but inactive. Do not resume 3D model development.
- Six-house documentation, invisible default weapon upgrades and alternate-skin offerings are obsolete.
- Previous PROJECT_STATUS and README milestone counts were stale; use current evidence below.

## NEXT
1. Finish Claude's camera checkpoint, compile it, then run the independent player suites and inspect overview/own-base/scouting/full-map/threat-warning screens.
2. Verify direct taps at 1280x720 and a wide phone safe area, including return-home, build/repair controls, pause and eliminated-player scouting.
3. Improve character animation/readability and mobile performance after this path is stable. Keep gameplay and visual layers separate.
4. Maintain platform abstractions; no store SDKs, purchases or fabricated online leaderboards.

## LAST TEST
- Astra ran Tools/Test-Core.ps1 on 2026-09-30: **124 passed** (19 core + 16 progression + 16 collection + 16 social + 12 ranking + 18 neighborhood + 27 contract). C# warnings treated as errors. Claude is adding camera tests after this count.
- Existing Unity checkpoint additionally reports 17 persistence checks (141 total). Astra did not start a competing Unity build while Claude was editing/building.
- Astra's independent copied preview at 1280x720 passed **Social, Collection and Match**, each followed by a **separate-process reload**, zero runtime errors. Evidence: TestResults/Assist-20260930-195930-25877f/verification.json and each suite's screenshots/logs. Overview and own-house screenshots inspected.
- Tested runtime assembly SHA256: A170E96D6A38A70FE4727877992E961CCA02B4B34356E5521ED55D69E5E78EFE. Source player: TestResults/UI-Pass/preview-build/ContainerDefense.exe. This identifies the tested binary, not all subsequent camera edits.
- New runner: Tools/Verify-Player.ps1 -Player <development executable>. It snapshots the player, uses unique isolated accounts, records binary identity, checks process exits/results/timeouts and never deletes old evidence or touches real saves.

## HANDOFF
- Stable source before camera work: fd378ab (Claude art pass). Earlier Claude milestones: f71d2ce (contracts/routes), e4e270d (last standing), edd6830 / 84293da (UI).
- Ownership while collaborating: Claude — runtime camera/HUD/art and associated tests; Astra — README, PROJECT_STATUS and Tools/Verify-Player.ps1. Do not stage or revert another agent's uncommitted files.
- Scene: Assets/Scenes/ContainerYard.unity. Map: Assets/Resources/FixedMap.asset. Save filename: account-v1.json, payload version 4; preserve unknown ownership and real profiles.
- Build command: Tools/Build-Windows.ps1 [-MilestonePreview]. Run only one Unity editor/build against this checkout. Never run multiple real-profile players concurrently.

## CLAUDE, WHILE ASTRA WAS OUT (2026-10-01)
Branch claude/continue-astra, local only. Astra left nothing uncommitted after 9709660; the buttons, mechanics and background-art rework was planned but had no code yet.
- **f2455fd TFT camera.**
  - Domain/MatchView: the whole neighbourhood while claiming, then your own base.
  - Read-only scouting of living players.
  - Full map from the minimap.
  - A red edge and GO HOME button when the boss lines up your house.
  - 9 MatchViewTests.
- **fa812a0 / db40987 / 4b67a36 HUD polish:** round portraits, house panel thumbnail, round minimap, icons.
- **3422133 Hand-built UI:**
  - opaque framed cards and one button system with pressed and disabled states
  - Lilita One headings (OFL, see Assets/Fonts/LICENSE.txt) and a live-text logo
  - padlock locked state
  - blurred menu background
- **3ecad5b Characters match the room art everywhere.**
  - vhi rejected the generated characters. The menu, portraits, collection, HUD and in-world characters now all use the nine-pose room atlases.
  - The atlases were de-fringed; originals are in Art/Source/Atlas-Originals.
  - The locked flat world clashed with the room characters, so the current world art stays and only the locked icons are wired in.
  - Details: Art/Source/Generated/v4-locked/VETTING.md.
- **45de420 Hand-made combat feedback, visual only:** per-weapon projectiles, impact stars, damage numbers, boss target ring, house pop, coins flying to the gold counter.
- **Checks:** 133 domain + 17 Unity persistence = 150 pass. The HUD audit is clean at 1920x1080, 1280x720 and 1920x886 with a simulated notch.
- **cc85835 World cleanup:** all six house colours are now one drawing (hand recolours of the blue house). Props are cleaned, and every prop and lamp has a contact shadow. Originals are in Art/Source/World-Originals. The existing north strip already matches this style, so it stays.
- **Builds:** the preview build is TestResults/UI-Pass/preview-build/ContainerDefense.exe, built from cc85835. Builds/Windows has not been rebuilt.
- **Open:**
  - A north-edge dressing strip and any remaining world art in the room style.
  - No audio exists, so there are no button sounds.
  - Real touch-device QA.
- **ebfb4b6 Sound:**
  - 16 clips from Kenney's CC0 packs (Assets/Audio/LICENSE.txt).
  - GameAudio: per-weapon shots with cooldowns, warnings, jingles.
  - Music/sound volume and mute in the pause screen and the title settings, saved as an added AccountData.Audio field; old saves get defaults.
  - No music loop: no CC0 pack used contains one.
- **927f867 Phone readiness:**
  - The mobile UI scale keeps 96 px targets at 48 dp or more.
  - Landscape-only rotation.
  - HUD audit clean at 1920x864, 1600x720, 1560x720 and 1280x720 (TestResults/Phone).
  - Every action has an on-screen control (gear for pause, arrows for spectating, tap to walk and claim).
  - Android Build Support is NOT installed for 6000.3.0f1, so there is no APK yet. Install it from Unity Hub (Installs > 6000.3.0f1 > Add modules > Android Build Support with SDK/NDK and OpenJDK).
- **Playtest sweep** (Tools/Run-PlaytestSweep.ps1; 70 seeds, all 7 characters, all 5 route kinds):
  - 0 crashes and 0 stuck matches.
  - **Matches are far too short:** the median is 61 s (about 36 s of combat), against the ~510 s target. With default rules the bots kill the 6,500 HP boss quickly.
  - Balance was not changed; this is vhi's call.
  - Character placements are even (average 3.43 to 3.58). In boss-defeat endings, placement among survivors follows house number, so first places are not a skill signal.
