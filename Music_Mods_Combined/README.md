# Music Mods Combined

Backup of my compatibility patch for Skyrim Special Edition music mods. On their own, these mods
overwrite each other's music lists, so whichever loads last silences the rest. This patch merges
all of their track lists, plus the vanilla soundtrack, into one light (ESL-flagged) plugin, and
weights them by my share rules: Melodies of Civilization leads in towns, Around the Fire in
taverns, Dawnguard Music Overhaul in Dawnguard areas, Nyghtfall in combat and at night, and
Chapter II / Celtic / Still / LORKHAN share the wilderness and dungeons by day (details in
`docs/patch/v4-design.md` and `v5-design.md`).
Every mod's own rules for when its music plays still apply: time of day, Dawnguard eclipse,
location, dragon or NPC combat.

**Needs the original mods.** This repo holds only my own work. It does not include any music mod's
plugins, music or archives.

## Mods it needs
| Mod | Plugin |
|---|---|
| Melodies of Civilization 1.01 | `Melodies of Civilization.esp` |
| Dawnguard Music Overhaul (Main + Combat modules) | `Dawnguard Music Overhaul.esp`, `Dawnguard Music Overhaul - Combat.esp` |
| Still (ESP version) | `Still.esp` |
| Celtic Music in Skyrim SE 2.1 + Dungeon add-on | `Celtic_Music_In_Skyrim_SE.esp`, `Celtic_Music_In_Skyrim_SE_Dungeon.esp` |
| Around the Fire 1.0 (taverns) | `Around the Fire.esp` |
| Nyghtfall (With Vanilla Music 2.0.1) | `NyghtfallMM_NonReplacer.esp` |
| Chapter II v4 (ESL) | `Chapter II - Soundtrack mod by Dreyma Music.esp` |

Optional: **LORKHAN - Soundtrack Replacer** (Nexus 115096). It replaces the vanilla music files and
has no music plugin, so the patch doesn't need it; with it installed, the patch's vanilla share plays
LORKHAN's music and counts as an equal partner (`docs/patch/v5-design.md`).

Not needed: `MelOfCiv - Chapter II Patch.esp` and `Still - Chapter II Patch.esp`. They were made
for Chapter II v3.5, and this patch covers the same merges for v4.

## Load order
The music mods in any order (Dawnguard Music Overhaul after Melodies of Civilization, which it
needs), then `Music_Mods_Combined.esp` **last**. In Vortex, set a "load after" rule on the patch
for the music mods if LOOT doesn't place it after them.

## Install
Zip the contents of `FOMOD/Music Mods Combined FOMOD/` (so `fomod/` and `Core/` sit at the top
of the zip) and install the zip through Vortex. The installer has one page. Then deploy and check the
load order.

## What's here
| Folder | Contents |
|---|---|
| `FOMOD/Music Mods Combined FOMOD/` | The installer, unzipped: `fomod/` config and `Core/Music_Mods_Combined.esp` |
| `tools/` | `build_combined.py` (builds the patch), `weighting.py` (share rules), `verify.py` (checks it), `esp.py` (plugin reader/writer), `load_order.txt`, `rules.json` (the percentages), `fixes.txt`, `exclude_tracks.txt` |
| `docs/analysis/` | What each mod touches, how tracks pick their time and place, problems found in the mods |
| `docs/patch/` | Design per version (`v1-design.md` to `v5-design.md`) and the generated merge reports |

## Rebuilding
Python 3, no extra packages. From inside `tools/`:

```
python build_combined.py <folder> --out Music_Mods_Combined.esp --report ../docs/patch/v5-merge-report.md
python verify.py <folder> Music_Mods_Combined.esp
```

`<folder>` must hold every plugin named in `load_order.txt`, including the game's `Skyrim.esm`, `Update.esm` and `Dawnguard.esm`. Without the
game files the build still runs, but vanilla tracks a mod removed won't come back. To add or remove
a music mod, edit `load_order.txt` (and its group in `rules.json`) and rebuild. To change who
leads where or any percentage, edit `rules.json`. `--rules none` builds the plain v0.3.0-style merge.

## Status
v0.5.0. Built and verified at the file level: 47 music types, 385 day/night track copies,
23,265 list entries; all references resolve, no source track is lost, and an independent recount
puts every music type within 7% of its share rules. **Not yet tested in game**; see the end of
`docs/patch/v4-design.md` for what only an in-game test can confirm.
