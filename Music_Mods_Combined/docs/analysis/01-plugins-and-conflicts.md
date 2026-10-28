# Music mods: what each plugin touches and where they collide

Plugins examined (from the archives in my `Music_Overhaul\Music_Mods_Used` folder). The two
Chapter II patches were kept for reference only and aren't part of my load order:

| Plugin | Short | ESL flag | Masters | Music types edited | Track records |
|---|---|---|---|---|---|
| `Melodies of Civilization.esp` (1.01) | MoC | yes | Skyrim.esm, Dawnguard.esm | 2 | 8 |
| `Dawnguard Music Overhaul.esp` | DMO | yes | Skyrim.esm, Melodies of Civilization.esp, Dawnguard.esm | 4 | 12 |
| `Dawnguard Music Overhaul - Combat.esp` | DMO-C | yes | Skyrim.esm, Dawnguard.esm | 1 | 3 |
| `Still.esp` (ESP version) | Still | no | Dawnguard.esm, Skyrim.esm | 11 | 6 |
| `Celtic_Music_In_Skyrim_SE.esp` (2.1) | Celtic | no | Skyrim.esm, Update.esm | 13 | 69 |
| `Celtic_Music_In_Skyrim_SE_Dungeon.esp` | Celtic-D | no | Skyrim.esm, Update.esm | 4 | 8 |
| `NyghtfallMM_NonReplacer.esp` (With Vanilla Music 2.0.1) | Nyght | yes | Skyrim.esm, Update.esm, Dawnguard.esm | 44 | 189 |
| `Chapter II - Soundtrack mod by Dreyma Music.esp` (v4 ESL) | ChII | yes | Skyrim.esm, Update.esm, Dawnguard.esm | 14 | 197 |
| `MelOfCiv - Chapter II Patch.esp` (for ChII 3.5) | MoC×ChII | yes | Skyrim.esm, Dawnguard.esm, ChII, MoC | 1 | 1 |
| `Still - Chapter II Patch.esp` (for ChII 3.5) | Still×ChII | yes | Skyrim.esm, Dawnguard.esm, ChII, Still | 8 | 2 |

## How Skyrim picks music
A **music type** (`MUSC`) is a situation: exploring the Reach, being in a town, a dungeon, combat,
a discovery sting. Locations, regions and the game engine choose the music type. Each music type
holds a list of **tracks** (`MUST`), and the game picks from that list at random. Repeats in the
list act as weight. Vanilla lists repeat silence tracks such as `_MUSExploreSILENT30TypeSpacer` to
leave gaps between pieces.

None of these mods edit cells, regions or locations. **Every one of them works only by rewriting
music type track lists**, so the only conflicts are on those lists. A music type record can only
have one winner, and whichever plugin loads last replaces everyone else's list. Without a patch,
most of these mods' music never plays.

## Which music types collide

| Music type | Edited by |
|---|---|
| `MUSExploreCoast`, `ForestFall`, `ForestPine`, `Marsh`, `Mountain`, `Reach`, `Snow`, `Tundra` | 5 each: Still, Celtic, Nyght, ChII, Still×ChII |
| `MUSTownTest` (town and city music) | 5: MoC, Celtic, Nyght, ChII, MoC×ChII |
| `MUSCastle` | 4: MoC, DMO, Celtic, Nyght |
| `MUSDungeon`, `MUSDungeonFort`, `MUSDungeonIce` | 3: Still, Celtic-D, Nyght |
| `MUSExploreDLCFalmerValley`, `MUSExploreDLCSoulCairn` | 3: DMO, Nyght, ChII |
| `MUSExploreSovngarde` | 3: Celtic, Nyght, ChII |
| `MUSCombatBossDLC1` | 2: DMO-C, Nyght |
| `MUSDungeonCave` | 2: Celtic-D, Nyght |
| `MUSDungeonDLCVampireCastle` | 2: DMO, Nyght |
| `MUSSovngardeHallofValor` | 2: Nyght, ChII |
| 27 others (combat, dread, reveal, reward, discovery, stinger, death, tavern, …) | 1: Nyght only (plus Celtic on the two Sovngarde chant types, ChII on `MUSExploreTestb`) |

The two Chapter II patches already merge Chapter II with Still and with MoC, but nothing merges
Celtic, Nyghtfall or the Dawnguard Music Overhaul with anything else.

## Loose files
No two mods ship a file at the same path, so Vortex reports no file conflicts between them. The
Celtic mods pack their music in BSAs; the others use loose `.xwm`/`.wav` files under
`music\<mod name>\`. (Nyghtfall's 645 MB archive was not opened; its plugin was extracted by hand.)
