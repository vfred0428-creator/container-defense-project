# Container Defense

An existing Unity/C# solo survival prototype: one local player and five independent bots race for **12 fixed container houses**. Each house has one owner, a personal wallet, a bed, a door and three rooftop weapon sockets. No teams, shared currency or revives.

## Run

Use Unity **6000.3.0f1** and open `Assets/Scenes/ContainerYard.unity`. Choose an unlocked character and press **PLAY**, or use **Practice Ranked** for local rank progression.

`Tools/Build-Windows.ps1` builds `Builds/Windows/ContainerDefense.exe`; `-MilestonePreview` builds `Builds/Milestones/ContainerDefense.exe`. Each build first runs the domain and persistence checks. Do not run two Unity builds against the same project at once. The separate UI preview lives under `TestResults/UI-Pass/preview-build`; its age must be checked before using it as verification evidence.

## Play

- During the 25-second preparation period, tap a free house to walk to it and claim it. Alternatively use WASD/arrows and E/Space near its door.
- Sleeping generates personal gold. Use the sleep/wake control or E/Space; sleeping players cannot walk.
- Upgrade **Bed** for income and **Door** for maximum HP. Use **Build** to place Gatling, Cannon, Slow or Rocket defenses in three visible rooftop sockets, then upgrade, move or sell them. An empty house has no invisible default gun.
- Weapons fire automatically only when built and in range. Repair restores up to 140 HP for 40 gold. Selling returns half the amount actually invested, rounded down.
- Tap a participant portrait to scout. Other houses are read-only; exact opponent gold stays private. Return home to issue build commands. The map shows houses and the live boss position.
- Escape pauses; Tab cycles living residents after elimination. Number keys 1/2/3 upgrade bed/door/first placed weapon.

The cloud boss starts at a seeded edge entrance, telegraphs targets, travels along the fixed road graph and attacks at most three times per visit. A visited house gets an 18-second protection window. Destroyed or vacated houses are skipped. Losing your house eliminates you permanently for that match.

A match ends when the boss dies, all houses fall, or one contestant remains after at least two claimed houses entered combat. Results record placements. Current prototype rewards surviving players on boss defeat as well as the last survivor; placement-based competitive scoring is not implemented. This is local practice, not online matchmaking.

## Characters and collection

| Character | Account level | Personal passive |
| --- | --- | --- |
| Milo | 1 | +8% gold generation |
| Lumi | 3 | +15% door maximum HP |
| Kiko | 5 | +10% weapon damage |
| Nori | 7 | Beds generate gold 12% faster |
| Pip | 9 | +12% movement speed |
| Mochi | 12 | 8% chance an upgrade costs half price |
| Yume | 15 | +10% build speed |

Exactly seven active skins exist: one Default per character. Historical cosmetic ownership remains in saves but retired skins cannot be equipped. Unlocks are permanent; seasons do not reset account level.

Stickers are signed 64-bit stacks. The one-time starter pack gives Bunny x52, Lucky Star x24, Cozy Cat x17, Good Job! x7 and Heart x38. Charisma measures unique collection ownership, not duplicate quantities or gameplay strength. The starter collection has 100 Charisma.

**Social** includes a profile, local practice gifting, a quantity selector, confirmation, notification and receipt history. Transfers validate identity, quantity, ownership, overflow and retry IDs, then atomically save sender, receiver, Popularity and receipt. The two practice inboxes are local data, not real online recipients. Popularity measures received gift value and is separate from Rank and Charisma.

**Practice Ranked** has Rookie, Scout, Defender, Vanguard, Champion, Ascendant, Celestial and Sovereign. The prototype uses five-star promotions, +1 for a surviving victory and -1 for a loss, tier floors and a Sovereign cap. RankPoints is stored for future rules. Ranked, Charisma and Popularity have distinct local row-based leaderboards containing actual recorded profiles.

## Architecture and saves

- `Assets/Scripts/Domain`: Unity-independent match authority, seeded route planner, issuer-bound commands, scouting snapshots, progression, inventory, social transactions and ranking.
- `Assets/Scripts/Runtime`: input, camera, 2D rendering, HUD, local persistence and development-only test drivers.
- `Assets/Resources/FixedMap.asset`: fixed houses and road graph. `DefaultMatch.asset`, `Characters.asset` and `Collections.asset` hold existing tuning/catalog data.
- `Assets/Resources/Art2D`: active sprites. `Art/Source/Generated/v2` retains Claude's latest source artwork. Old 3D factories remain inactive.

`ISaveService` / `LocalSaveService` persist `account-v1.json` in Unity's application data folder. Payload version 4 migrates versions 1-3 while retaining XP, unlocks and unknown ownership. Checksums, atomic replacement and backup recovery protect local writes; unreadable/newer files are preserved. Failed saves retain in-memory progress and show a retry. Cloud sync and cross-device authority are not implemented. Never run multiple real-profile game instances simultaneously.

## Verification

Run `Tools/Test-Core.ps1` for warning-as-error domain compilation and the regression suites. Unity's **Container Defense > Run Core Checks** additionally exercises JSON/file persistence. Use the reported counts in `PROJECT_STATUS.md`; older test folders describe older binaries.

Run all player regression checks and separate-process reloads against a built development player:

```powershell
.\Tools\Verify-Player.ps1 -Player '.\Builds\Milestones\ContainerDefense.exe'
```

This copies the player into a fresh `TestResults/Assist-*` folder, records the runtime assembly hash, and uses isolated test accounts. It checks Social, Collection and Match in sequence, failing on timeouts, missing results or nonzero exits. It never starts Unity or deletes previous evidence. `-Suites Social,Collection` narrows the run. Windows stay visible because hidden captures can be black. The smoke drivers exercise actual session/domain APIs; they do not replace manual tap testing.

`Tools/Capture-UiShots.ps1` captures the UI at desktop and wide-phone aspect ratios, including simulated notch insets. Screenshot coverage is not Android device validation.

See `PROJECT_STATUS.md` for current work, evidence and remaining limitations. Online multiplayer, backend gifting, platform SDKs, purchases and Android/Steam release readiness remain out of scope for this checkpoint.
