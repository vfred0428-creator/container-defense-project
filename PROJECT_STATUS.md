# Container Defense — project status

Updated 2026-09-30. Latest Photo 1–5 references define the 2D / 2.5D direction.

## 12-HOUSE NEIGHBORHOOD MILESTONE (Claude continuation of Astra's work, branch claude/continue-astra)
- Fixed MapDefinition: 12 houses, 20 road nodes, orthogonal edges derived from geometry. Load validates connectivity, that route steps are road edges, that each house is within 7 units of a node, and a 4 s telegraph minimum. Pathing uses deterministic Dijkstra with lower-id tie-breaks.
- Pure seeded BossRoutePlanner: only living occupied unprotected houses; least-visited first for fairness; ordered along the route; one other target before a repeat; 1–2 available houses fall back to Straight.
- The boss clock carries leftover time across phases and starts exactly at the preparation deadline. Results match at 30/60/144 steps per second. Travel walks node by node with no corner cutting. Protection is 18 s of match time and starts at visit end.
- Targets are re-checked every step and on arrival. A target that empties, dies or becomes protected before the first hit is skipped: no visit, no protection, and a fresh 4 s telegraph for the next target. If every house is protected, the boss waits (phase Waiting) until the first protection lapses, including with a lone survivor.
- Commands go through PlayerCommands, bound by MatchSimulation.CommandsFor(id). GameSession and bots never pass an issuer id. Validation covers owner, slot 0–2, slot state, build state and personal wallet. Wallets saturate at 1e12. Sell refund is half of what was actually paid.
- Scout(viewer) returns value-only HouseScout data. Other players' gold appears only as a coarse WealthBand. Bots and the HUD house board read it.
- Elimination (house destroyed, or the new domain-only TryForfeit) vacates the house: its weapons stop, it is never targeted, and late commands are rejected. Finish is idempotent and cancels target, route and timers. Weapons resolve before boss attacks within a step.
- Checks: 124 domain (Tools/Test-Core.ps1, incl. 27 MatchContractTests) + 17 Unity persistence = 141 pass. Smoke + reload evidence (pre-last-one-wins build): TestResults/M6-Neighborhood. The Windows player was not rebuilt for last one wins, at vhi's request.
- LAST ONE WINS (vhi decision 2026-09-30): if two or more houses enter combat, the match ends (Victory, EndReason LastStanding, WinnerId) as soon as one occupied house remains, whether by boss elimination or TryForfeit. Endings are judged at step end; weapons resolve first, so a boss killed in the same step is BossDefeated. If no house remains, the result is AllFallen (Defeat). Placement: standing first, then later EliminatedAt; ties go to the lower house number, and players with no house come last by player id. A match that enters combat with a single house is still a solo boss fight. The boss Waiting path remains, because two or more living houses can all be protected at once. TryForfeit is logic-only (kept for future leave/disconnect).

## UI AND MAP PASS (2026-09-30, in progress: after-screenshots pending)
- Before shots: TestResults/UI-Pass/before at 1920x1080, 1280x720 and 1920x886. 1920x886 has the 2340x1080 aspect scaled to the 1920-wide desktop. They were captured by Tools/Capture-UiShots.ps1 from the existing player. The Editor harness Assets/Editor/UiScreenshots.cs needs an activated Unity Editor license, which is currently missing.
- HUD stays IMGUI, not replaced with uGUI; it now applies the spec:
  - HudTheme: 1920x1080 reference, match 0.5 (geometric mean). Navy #1B2238 panels with a 2px #3A4670 border, 20px radius and soft shadow. Bevelled buttons: green #39C46A for confirm, orange for play, slate secondary. The only glow is a 4px gold #FFD04A selection ring.
  - Nunito at 56/36/28/22, never below 22. Thousands separators and mm:ss.
  - Layout uses Cut (rect cutting), so sibling regions cannot overlap. Everything interactive is inside Screen.safeArea. HudAudit logs overlaps, out-of-safe-area controls and text that would clip, in development builds.
- Match HUD:
  - Top-left: portrait strip in house order with HP bars and house badges.
  - Top-centre: boss bar with HP numbers.
  - Top-right: gold and timer pills, gear, and a 4x3 minimap.
  - Left column: map/focus, my house, prev/next, and my room.
  - Bottom panel (260px, 24% at 1080p): house summary, the four house actions, and three socket cards with an inline weapon picker.
  - One toast slot above the panel holds at most two lines.
- Map: re-laid as 4 houses x 3 rows on a 5x4 road grid (still 12 houses, 20 orthogonal nodes, and the same route kinds). This fills the wide band between the HUD regions. Houses use a uniform scale (previously stretched 1.5x). Roads and ground are tiled; boss entry chevrons sit at the ring edge. Rooftop number plaques include owner HP and hide under HUD panels. The camera fits the neighbourhood into the space the HUD leaves free. FixedMap.asset matches MapDefinition.Default(), which RunChecks enforces.
- Generated art: Higgsfield originals are in Art/Source/Generated. Resized and seamless versions are in Assets/Resources/Art2D/Generated: ground_tile, road_straight, road_cross, boss_entry_marker, and icons coin, timer, gear and heart. Each has a procedural fallback, so removing a file restores the placeholder.
- VIEW MY ROOM was unreachable after the 12-house HUD change; it is restored as a MY ROOM button while sleeping.

## WORKING
- M1 preserved: local human + five independent bots, six exclusive houses, movement/claiming, sleeping income, personal upgrades, boss targeting/damage, elimination, spectating, win/loss.
- M2 preserved: seven personal passives, permanent XP/level/unlocks at 1/3/5/7/9/12/15, selection and save recovery.
- M3 current scope: exactly seven active Default skins, five stackable sticker types with 64-bit quantities, unique-collection Charisma, one-time sticker starter pack and persistence.
- 2D presentation: seven nine-pose atlases, shared SpriteSet / VisualController / AnimationController, two-frame running, state poses and subtle breathing; shared atlas portraits.
- Layered illustrated yard, six color-coded numbered house sprites, 2D cloud boss, hit feedback and reused weapon tracers.
- Home/collection UI refreshed with one font family, fixed readable font weights, restrained navy panels and yellow actions. Locked characters show unlock levels and passives.
- VIEW MY ROOM while sleeping: shared cozy interior, selected resident's sleep pose, rising income effects. VIEW YARD returns to the encounter.
- Historical retired skin IDs remain owned in saves; active equipment falls back to Default, with a dedicated migration regression.

## PARTIAL
- Visual migration is a playable first pass, not final animation polish. Atlas frame alignment/silhouettes need production cleanup (notably some hair/effect edges and limited run directions).
- Interior is one shared bunny-themed room; personalized house decor, layered door animation, detailed attack effects and audio remain.
- HUD is still runtime IMGUI with desktop keyboard controls. Mobile touch/safe-area support, profiling and Android device builds are not validated.
- One boss encounter (Wave 1), no expanded wave system. Local simulation only; no online multiplayer or server authority.
- M4 local prototype implemented: profile names/IDs, match stats, multi-copy gifting, separate Popularity, receipts, unread inbox and paginated history. Practice inboxes are explicitly local; no online friends or backend connection.
- Gift authority validates quantities, sender binding, capacity/overflow and idempotency; debit + credit + Popularity + receipt share one atomic save. The 1,000-receipt local ledger fails closed when full. Cloud auth, cross-device concurrency, anti-abuse and online delivery remain future work.
- M5 local prototype working: eight solo rank tiers, five-star promotions, tier floors, highest rank, points/season/stats persistence, distinct casual and Practice Ranked entry points, and separate local Ranked/Charisma/Popularity row tables.

## BROKEN
- No observed compile errors or failing checks at this checkpoint.
- Latest milestone preview: 96 checks pass (19 core + 16 progression + 16 collection + 16 social + 12 ranking + 17 persistence), no C# warnings.
- M5 full rendered match and separate-process reload pass: claiming, timed upgrades, combat, victory/elimination, once-only rank/profile results, XP/unlock persistence and all three leaderboard views.
- M4 rendered smoke and separate-process reload pass at 1440x900 / 1280x720: profile, ten-copy transfer, replay protection, incoming notification, Popularity, history and in-match gate. Screenshots reviewed.
- Rendered collection and full-match tests pass, including separate-process account/collection reloads. Screenshots checked at 1440x900 and 1280x720.
- Final UI draw-order correction manually verified through collection navigation and locked Yume inspection. Test accounts stayed isolated.

## OBSOLETE
- ResidentFactory, ResidentDetails, ToyFactory, YardBuilder: preserved but inactive; ArenaView no longer creates 3D character meshes, lights or portrait render targets.
- Earlier 3D-style illustrations moved outside Resources to Assets/Art/Legacy3D, excluded from player content.
- Alternate-skin offerings retired. Do not reintroduce them or improve the old 3D characters.

## NEXT
1. Latest 2026-09-30 brief supersedes the six-house presentation: migrate to a fixed high-angle 12-house map, route graph, boss target protection, visible weapon sockets and read-only scouting.
2. Claude completed a gifting audit and ranking recommendation. Applied confirmation/retry identity, one save lock, overflow checks and practice labels. See Tools/Claude-Review-Handoff.md for reviewed findings and deferred policy suggestions.
3. Continue 2D animation/frame cleanup, attack readability, mobile HUD/input, sound hooks and texture/device profiling.
4. Platform interfaces only; no store SDKs or purchases yet.

## Handoff
- Scene: Assets/Scenes/ContainerYard.unity. GameSession constructs the view; domain gameplay is independent of Unity rendering.
- Active art: Assets/Resources/Art2D. Generation method and exact prompts: Tools/2D-Artwork-Prompts.md.
- Save filename: account-v1.json, payload version 4 (migrates v1/v2/v3). Profile/recipient balances/receipts/rank live together in the envelope. Do not reset real accounts or erase unknown ownership.
- Build: Tools/Build-Windows.ps1 -> Builds/Windows/ContainerDefense.exe.
- Parallel preview build: Tools/Build-Windows.ps1 -MilestonePreview -> Builds/Milestones/ContainerDefense.exe, preserving the user's open older executable. Do not run both builds against the same real save concurrently.
- M4 evidence: TestResults/M4-Social/{result,reload-result}.txt and screenshots. Smoke runs use isolated account paths.
- M5 evidence: TestResults/M5-Ranked/{result,reload-result}.txt and screenshots.
- Domain checks: Tools/Test-Core.ps1.
- Evidence: TestResults/Pivot-Stable-Collection/{result,reload-result}.txt and TestResults/Pivot-Stable-Match/{result,reload-result}.txt; latest compiler log TestResults/unity-build.log.
- Run smoke players visibly for screenshots. Hidden-window runs produce black captures even when the simulation passes.
- Baseline committed before pivot. The user's normal game is open for review; do not close/rebuild over it without accounting for current use.

