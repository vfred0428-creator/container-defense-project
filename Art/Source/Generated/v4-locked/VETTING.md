# Locked-style art vetting (2026-10-01)

Reference: milo-master.png (candidate b, cleaned). Checked for:
- matching outline colour and weight
- flat cel shading, no gloss, glow or gradients
- roster design
- hands
- stray text

| File | Verdict | Notes |
|---|---|---|
| milo-a / milo-c | not used | a: hammer reads as a pickaxe; c: hammer cropped, uneven outline |
| milo-master (from b) | keep | halo removed, trimmed |
| lumi | keep | cloud hair, blue jacket, bunny plush and toy blaster all on-model |
| kiko | keep | black cap with star, bunny patch, cannon on shoulder |
| nori | keep | pink curls, flower and star clips, crate of gold |
| pip | keep | goggles, green scout outfit, paper plane |
| mochi | keep | cream sheep hair, overalls, wrench. Holds a "50% OFF" ticket: intentional text that matches the half-price passive, so kept |
| yume | keep | purple curls, flowers, lantern staff |
| house-blue | keep | recoloured by hand (body hue only) into pink, yellow, purple, teal and red |
| house-damaged | keep | used for damaged and destroyed blue houses |
| plaza, boss, minion | keep | |
| ground, road-straight, road-cross | keep | tiled; ground made seamless; road cropped to its band and wrapped horizontally |
| dock-edge | keep | wrapped horizontally |
| menu-bg | keep | no numbers on houses; blurred and darkened behind the UI in game |
| weapons-sheet | keep | sliced: gatling, cannon, slow, rocket |
| props-sheet | keep | sliced: crate, barrel, plant, lamp, cone, pallets |
| icons-sheet | keep | sliced: coin, gem, timer, gear, heart, bed, door, repair, skull, house, up, lock |

**Rejects:** none.

**Gaps:** the old vinyl-style north container strip was retired (moved to Art/Source/Generated/v2/edge_strip-ingame.png). A locked-style north-edge strip would complete the yard border.

**Cleanup applied to every keeper:**
- alpha below 200 cleared, which removes the baked dark halo
- trimmed to content with even padding
- characters capped at 512 px tall
- portraits cropped from the full bodies: head and shoulders, top 52% of the figure, squared

## Update 2026-10-01: characters rejected (vhi)
vhi: the characters "went way too far off script" and must match the ones inside the rooms.
- **Rejected, kept here, not wired:** milo-master, lumi, kiko, nori, pip, mochi and yume.
- **Source of truth for every character:** the nine-pose room atlases (Assets/Resources/Art2D/<name>_default.png). The room view, menu, character select, collection, portraits, HUD and the in-world characters all draw from them now.
- **The v2 body_/portrait_ overrides** are removed from Resources; their originals are still in Art/Source/Generated/v2.
- **Atlas cleanup only:** I removed soft alpha and the red/yellow matting fringe at the edges, and the design is unchanged. The originals are backed up in Art/Source/Atlas-Originals.

## World decision 2026-10-01
I compared the room characters on the locked flat world with the same view on the current world: TestResults/Art-Locked/world-compare-*.jpg.
- **The flat outlined world clashes** with the glossy, painted room characters, so the current world art stays: houses, ground, roads, dock, edge strip, boss, minions, weapons, props and the menu background.
- **Wired from the locked set:** the icons only (coin, timer, gear, heart, bed, door, repair, skull, house, up arrow), plus the new padlock and gem.
- **Processed but unused:** the locked world sprites are kept, trimmed and recoloured, in Art/Source/Generated/v4-locked/sliced.
