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
