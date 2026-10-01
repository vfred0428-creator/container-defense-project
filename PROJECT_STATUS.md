# Container Defense - project status
Updated 2026-10-01. Branch: claude/continue-astra (local only).

## WORKING
- Preserved Claude's core: six solo residents, twelve houses, claim race, personal economy, bed/door upgrades, three weapon slots on eight yard pads, boss routes, elimination, spectating and placement results.
- Seven account-level unlocks and one default skin each; persistent account, stackable stickers, collection charisma, local gifting/popularity/profile, practice ranked and local leaderboards. No team mechanics.
- Claude's own-base board, yard editor, atlas characters, hand-built HUD, audio/settings and music remain active.
- Continued from 32c3972 and Claude's unfinished room runtime edits. Room entry/exit, sleep/awake poses, station errands, door/bed/weapon overlays, boss window/warning, forced yard view during attack and delayed room return are integrated.
- Room Bed / Door / Weapons cards use real rules. Bed/door purchases can queue, cancel and show construction progress; the weapon action opens the existing yard flow. Duplicate purchase taps are guarded.
- Fixed queue cancellation buying an upgrade, stale room effects across matches, character/door transforms at scaled resolutions and clicks through pause/results overlays.
- Fixed a missed balance migration: DefaultMatch.asset now uses 77,500 boss HP, matching Claude's five-minute tuning. The build checks asset/code agreement.

## PARTIAL
- Launch/resource preparation and welcome flow are still pending. Resources are bundled locally; there is no download service or online login yet.
- Additional character-selection transition polish remains pending. Current Claude artwork and roster were preserved.
- Presentation is still IMGUI. Real Android touch, device performance and release readiness remain unverified. Android Build Support stays uninstalled per the existing instruction.
- Social, ranked and account services are local prototypes; no multiplayer authority, cloud accounts, real recipients or store SDK integration.
- Interior uses one shared room with live overlays. Bespoke per-character rooms and held-card previews remain future polish.

## BROKEN
- No outstanding compile or runtime errors in this checkpoint's checks. Earlier failed captures/runs are retained as diagnostic evidence; use the final evidence below.

## OBSOLETE
- Legacy 3D character presentation is inactive. Do not develop it further.
- Earlier generated bodies rejected by the user, six-house layouts, alternate skins and old camera ownership notes are obsolete. Claude is no longer actively editing this checkpoint.

## NEXT
1. Implement a small truthful launch/preparation flow with retry/error states using bundled assets, then smooth character selection. Preserve the current cast and gameplay.
2. Validate real touch-device layouts/performance when Android tooling is authorized; do not call the game launch-ready yet.

## VERIFIED CHECKPOINT
- Unity development preview build succeeds. 155 domain + 17 persistence checks pass (172 total): TestResults/unity-core-results.txt and TestResults/interior-build.log.
- Full Match player run and separate-process account reload pass: TestResults/Assist-20261001-050356-b898f9/verification.json. Includes real room entry, station purchase/double-tap, queue/cancel, scouting guards, claim/sleep, combat, restart, elimination and persistence. This run preceded the final queued-label layout-only correction.
- UI audit: zero warnings at 1280x720 and 1920x886 with 108px simulated side insets: TestResults/Interior-final-20261001. Final queue-layout run captured 29 screens at 1280x720 with zero warnings. Room sleep/awake, boss warning, scaled turning and queued controls visually inspected.
- Current preview: TestResults/UI-Pass/preview-build/ContainerDefense.exe. Builds/Windows is unchanged. Runtime DLL SHA256: 7A555B1427E8CE471972D55AA54A351821785F3FA3722570FD910FBB2FD4E0EB.
- Verify-Player.ps1 now detects the smoke-output marker at either UTF-16 byte alignment. It still requires smoke drivers and isolated accounts. MatchSmokeDriver now waits for an actual scouted base and sufficient purchase gold instead of assuming fixture timing.
- Run one Unity editor/build per checkout. Keep real account files intact. Test players use isolated accounts under TestResults.
