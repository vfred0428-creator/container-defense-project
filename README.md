# Container Defense — 2D continuation

Standalone Unity/C# local survival prototype. One human and five independent bot competitors race for six exclusive container houses. There are no teams, shared gold, revives or shared upgrades.

## Run

Open this folder in **Unity 6.3 LTS (6000.3.0f1)**. Open `Assets/Scenes/ContainerYard.unity`, press Play, choose an unlocked character, then **PLAY**. The arena uses layered illustrations and seven 2D sprite atlases; gameplay remains the existing independent simulation.

For a Windows executable, use **Container Defense → Build Windows Playtest** or run `Tools/Build-Windows.ps1`. The output is `Builds/Windows/ContainerDefense.exe`.

## Controls

- **WASD / arrow keys:** move around the yard.
- **E / Space:** claim a nearby free house; press again to sleep. Wake with the same key.
- **1 / 2 / 3** or HUD buttons: upgrade your bed / door / weapon.
- **Tab:** cycle living residents after elimination.
- **Escape:** pause, resume, restart or return to the title.

Claim before the 25-second preparation timer expires. Sleeping earns gold; waking stops income. Weapons fire automatically during combat. Bed upgrades increase income, door upgrades add maximum and current HP by the same amount, and weapon upgrades increase damage. The boss attacks one door until it breaks, then chooses another living house. Surviving residents win when the boss dies; eliminated residents remain eliminated. All houses lost ends the match in defeat.

## Tuning and architecture

### Characters and account progression

| Character | Unlock level | Personal passive |
| --- | --- | --- |
| Milo | 1 | +8% gold generation |
| Lumi | 3 | +15% door maximum HP |
| Kiko | 5 | +10% weapon damage |
| Nori | 7 | Bed generates gold 12% faster |
| Pip | 9 | +12% movement speed |
| Mochi | 12 | 8% chance to pay half price for an upgrade |
| Yume | 15 | +10% upgrade build speed |

Locked cards show their required level and can be inspected without equipping them. Unlocks are permanent and earned through play. Each passive affects only its owner. Accepted upgrades occupy one builder for 1.5 seconds (1.5 / 1.1 seconds for Yume). Mochi must afford the listed price before the discount roll; rejected purchases cannot reroll it.

Completed matches award 30 participation XP to players who claimed a house, plus personal combat survival XP (1/second, capped at 180), damage XP (1 per 30 damage, capped at 100), and 75 XP for surviving a victory. Abandoned matches and unclaimed spectators earn no XP. Results show XP and new unlocks. Each match can reward the account once.

`Assets/Resources/Characters.asset` contains the seven definitions, passives, unlock levels, XP curve and reward tuning. The default account cap is level 100; all characters unlock by level 15. The first level requires 120 XP and each subsequent requirement increases by 40 XP.

Account XP, derived level, permanent unlocks and selected character save to `Application.persistentDataPath/account-v1.json`. Saves use an atomic replacement, checksum and backup recovery. Unreadable or newer save files are preserved rather than overwritten. Save failures display a retry option; progress remains in memory until a save succeeds. This is device-local persistence, not cloud sync or anti-cheat.

Edit `Assets/Resources/DefaultMatch.asset` in Unity to tune preparation, movement, economy, upgrades and boss statistics. That ScriptableObject is included; fallback defaults live in `MatchRules.cs`. The engine-independent domain owns claims, purchases, income, combat and outcomes. Unity handles input, rendering and HUD. Bots use the same validated commands as the human. A future server can own that command boundary, but this milestone has no networking or security guarantee.

### Collection

Open **COLLECTION** from character selection. The **Skins** tab supports previews, ownership and a separate equipped skin for each character. Equipment requires the character to be unlocked and the skin to be owned. Appearances carry into the selection screen and live matches, with no changes to passives or combat values.

There are exactly seven active skins, one Default per character. Claim the free starter sticker pack once: Bunny x52, Lucky Star x24, Cozy Cat x17, Good Job! x7, Heart x38. Historical alternate-skin ownership stays in saves but is excluded from the current catalog and equipment.

`Assets/Resources/Collections.asset` configures skin ownership IDs, character assignments, rarity, appearance colors, Charisma values, sticker metadata and starter contents. Sticker quantities use signed 64-bit integers; invalid changes and overflow are rejected. The **Stickers** tab shows exact quantities and item details. Gifting is deferred to Milestone 4.

Charisma is the sum of each owned skin's value and each owned sticker type's value, once per type. Duplicate stickers do not multiply it; spending the last copy removes that type's contribution. Default skins contribute zero. The starter collection totals 100 Charisma. The score is recalculated from inventory, never treated as combat power or rank.

Save schema 2 includes owned skins, per-character equipment, sticker stacks, the starter receipt and calculated Charisma. The existing save filename remains unchanged. Milestone 2 saves migrate while retaining XP, character unlocks and selection. Unknown inventory IDs are retained so temporarily removed content does not erase ownership.

## Verification

`Tools/Test-Core.ps1` compiles the domain with warnings treated as errors and runs 51 regression scenarios using the Windows .NET Framework compiler. **Container Defense → Run Core Checks** also runs 12 Unity JSON/filesystem save scenarios. All 63 run before Windows builds.

A development player accepts `--smoke-test --smoke-output "C:\absolute\output"` to exercise selection, claim, sleep, timed upgrades, pause, combat, XP rewards, saved progression, match restart, elimination and spectating, recording screenshots and a result. Run it with a visible window for screenshot capture. Test saves are isolated inside the output directory. Adding `--smoke-seed-account` seeds a new test account near Lumi's unlock boundary. After that test, launch a new process with the same output directory and `--smoke-reload-only` to verify Lumi, XP and selection survive an application restart. Test switches are excluded from release builds.

Add `--collection-smoke-test` with a fresh isolated output directory to exercise starter claims, skins, equipment gates, live appearance, large sticker stacks and Charisma. A second process using that directory and `--collection-smoke-test --collection-reload-only` verifies collection persistence. This test adds a large stack only to its isolated test account.

## Scope

Milestones 1–3: illustrated 2D presentation, one arena, local competitors, desktop HUD, seven characters with personal passives, account XP, level unlocks, cosmetic collections, Charisma and local persistence. Gifts, Popularity, profiles, ranks, backend services, storefront SDKs, final art/audio and mobile controls remain deferred to their approved milestones. Android remains a future build/input target, not a tested deliverable here.

See PROJECT_STATUS.md for the audited milestone state. The seven 3x3 character atlases, layered yard, house atlas, boss and shared cozy interior are in Assets/Resources/Art2D. Runtime animation supports two-frame running and expressive state poses with subtle breathing. VIEW MY ROOM while sleeping opens the interior; VIEW YARD returns to the encounter. The current HUD remains IMGUI and needs mobile controls/device validation. Old 3D artwork is archived outside Resources in Assets/Art/Legacy3D; legacy procedural factories are inactive. Generation prompts are in Tools/2D-Artwork-Prompts.md. Nunito uses fixed 600/800 weights instantiated from the bundled OFL variable source for reliable Unity rendering.
