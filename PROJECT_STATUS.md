# Container Defense — continuation status

Updated 2026-09-29. Official direction: 2D / 2.5D, latest five Photo references.

## WORKING
- M1: deterministic solo simulation, six independent players/houses, movement, first-valid claim, sleep income, upgrades, boss targeting/attacks, elimination/spectating and results.
- M2: seven personal passives, account XP/unlocks at levels 1/3/5/7/9/12/15, permanent selection and progression.
- M3: extensible collection catalog, long sticker quantities, unique-collection charisma, equipment validation, atomic local saves/checksums/backups and v1 migration.
- 50 domain regression checks pass at audit. Previous Unity build also passed 12 persistence checks. Windows development build available.

## PARTIAL
- M3 was completed against old scope: seven defaults PLUS three variants. Must migrate active catalog to defaults only while retaining existing save data.
- Runtime IMGUI screens are functional desktop prototypes. No mobile controls or platform integrations.
- Character motion is transform bob/rotation, not a 2D animation pipeline.

## BROKEN
- No failing domain check found. Fresh executable baseline playtest running; results in TestResults/Pivot-Baseline.

## OBSOLETE
- Procedural 3D residents, 3D portrait studio, primitive boss and old vinyl-style generated portraits.
- Three alternate skin offerings. Preserve extensible definitions and historical ownership rather than erase saves.

## NEXT
1. Preserve baseline and validate executable. Replace presentation without changing simulation.
2. Reference-derived 2D cast/animation, layered container yard, cloud boss, restrained UI; exactly seven active default skins.
3. M4 has NOT started: gift service/validation/history, popularity, profile and notifications.
4. M5 has NOT started: solo ranks and clearly identified local leaderboard prototype.
5. Mobile controls, optimization, sound hooks, platform abstraction and device validation.

## Structure / handoff
- One scene: Assets/Scenes/ContainerYard.unity. GameSession builds runtime views; no authored prefabs or animation controllers.
- Domain scripts are Unity-independent. Resources/Characters, Collections and DefaultMatch are ScriptableObject configs.
- Test: Tools/Test-Core.ps1. Full compile + persistence checks + Windows build: Tools/Build-Windows.ps1.
- Automated player: --smoke-test --smoke-seed-account --smoke-output <isolated directory>. Never seed real account data.
- Account file name remains account-v1.json; current payload version is 2. Unknown collection IDs are retained for compatibility.
