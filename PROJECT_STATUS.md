# Container Defense — project status

Updated 2026-09-29. Latest Photo 1–5 references define the 2D / 2.5D direction.

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
- M5 ranked progression and leaderboards are next.

## BROKEN
- No observed compile errors or failing checks at this checkpoint.
- Latest milestone preview: 83 checks pass (19 core + 16 progression + 16 collection + 16 social + 16 persistence), no C# warnings.
- M4 rendered smoke and separate-process reload pass at 1440x900 / 1280x720: profile, ten-copy transfer, replay protection, incoming notification, Popularity, history and in-match gate. Screenshots reviewed.
- Rendered collection and full-match tests pass, including separate-process account/collection reloads. Screenshots checked at 1440x900 and 1280x720.
- Final UI draw-order correction manually verified through collection navigation and locked Yume inspection. Test accounts stayed isolated.

## OBSOLETE
- ResidentFactory, ResidentDetails, ToyFactory, YardBuilder: preserved but inactive; ArenaView no longer creates 3D character meshes, lights or portrait render targets.
- Earlier 3D-style illustrations moved outside Resources to Assets/Art/Legacy3D, excluded from player content.
- Alternate-skin offerings retired. Do not reintroduce them or improve the old 3D characters.

## NEXT
1. M5: solo Rookie–Sovereign rank prototype and separate clean ranked/Charisma leaderboards; clearly distinguish local data from live service data.
2. Review Claude's delegated gifting audit and ranking recommendation; integrate applicable findings without changing the user's fixed passives or adding unrequested level gates.
3. Continue 2D animation/frame cleanup, attack readability, mobile HUD/input, sound hooks and texture/device profiling.
4. Platform interfaces only; no store SDKs or purchases yet.

## Handoff
- Scene: Assets/Scenes/ContainerYard.unity. GameSession constructs the view; domain gameplay is independent of Unity rendering.
- Active art: Assets/Resources/Art2D. Generation method and exact prompts: Tools/2D-Artwork-Prompts.md.
- Save filename: account-v1.json, payload version 3 (migrates v1/v2). Profile/recipient balances/receipts live together in the envelope. Do not reset real accounts or erase unknown ownership.
- Build: Tools/Build-Windows.ps1 -> Builds/Windows/ContainerDefense.exe.
- Parallel preview build: Tools/Build-Windows.ps1 -MilestonePreview -> Builds/Milestones/ContainerDefense.exe, preserving the user's open older executable. Do not run both builds against the same real save concurrently.
- M4 evidence: TestResults/M4-Social/{result,reload-result}.txt and screenshots. Smoke runs use isolated account paths.
- Domain checks: Tools/Test-Core.ps1.
- Evidence: TestResults/Pivot-Stable-Collection/{result,reload-result}.txt and TestResults/Pivot-Stable-Match/{result,reload-result}.txt; latest compiler log TestResults/unity-build.log.
- Run smoke players visibly for screenshots. Hidden-window runs produce black captures even when the simulation passes.
- Baseline committed before pivot. The user's normal game is open for review; do not close/rebuild over it without accounting for current use.

