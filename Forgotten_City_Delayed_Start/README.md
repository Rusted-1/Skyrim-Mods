# Forgotten City – Delayed Start

Backup of my patch for **The Forgotten City** (Skyrim Special Edition). Normally the courier
brings Cassia's letter as soon as you're level 5. With this patch the courier waits until
**level 25**. It's one light (ESL-flagged) plugin with two records. The level is stored in a global,
so you can change it in game:

```
set FCDS_CourierLevel to 30
```

Only the courier is delayed. You can still find the Forgotten Ruins on your own at any level.

This replaces "Forgotten City - Delayed Start" (Nexus 56958), which made the same kind of edit but
in my game the courier still arrived at level 5. See `docs/analysis/02-old-delayed-start-mod.md`.

**Needs the original mod.** This repo holds only my own work. It does not include The Forgotten
City or any other author's plugins or archives.

## Mods it needs
| Mod | Plugin |
|---|---|
| The Forgotten City 1.8 (Nexus 1179) | `ForgottenCity.esp` |

Works alongside these (none of them edits the record this patch changes, see
`docs/analysis/03-fix-mods.md`):
The Forgotten City Entrance Extended, The Forgotten City Music Bug Fixer, The Forgotten City - Good
Ending Tweaks.

**Uninstall the old "Forgotten City - Delayed Start.esp"** if you have it.

## Load order
After `ForgottenCity.esp`. Its position relative to the fix mods doesn't matter.

## Install
Zip the contents of `FOMOD/Forgotten City Delayed Start FOMOD/` (so `fomod/` and `Core/` sit at
the top of the zip) and install the zip through Vortex. Deploy, then check in the Plugins tab
that `ForgottenCity_DelayedStart.esp` is **enabled**.

Install it on a new character, or on a save that hasn't been sent the letter yet. If a save has
already queued the courier, the letter still arrives.

## Check it's working
In the console, `GetGlobalValue FCDS_CourierLevel` should print `25`. If the console says the
global doesn't exist, the plugin isn't loaded.

## What's here
| Folder | Contents |
|---|---|
| `FOMOD/Forgotten City Delayed Start FOMOD/` | The installer, unzipped: `fomod/` config and `Core/ForgottenCity_DelayedStart.esp` |
| `tools/` | `build_patch.py` (builds the plugin), `verify.py` (checks it against ForgottenCity.esp), `esp.py` (plugin reader), `bsa.py` (SE BSA extractor) and `pexdis.py` (Papyrus .pex disassembler), both used for the analysis |
| `docs/analysis/` | How the quest starts, the old delayed-start mod, what the fix mods touch |
| `docs/patch/` | `v1-design.md`: the records, the checks, the in-game test plan |

## Rebuilding
Python 3, no extra packages for building (`bsa.py` needs `lz4`). From inside `tools/`:

```
python build_patch.py --out "../FOMOD/Forgotten City Delayed Start FOMOD/Core/ForgottenCity_DelayedStart.esp" --level 25
python verify.py "../FOMOD/Forgotten City Delayed Start FOMOD/Core/ForgottenCity_DelayedStart.esp" <path to ForgottenCity.esp> --level 25
```

## Status
v1.0.0. Built and verified at the file level: all 20 checks in `verify.py` pass against
ForgottenCity.esp 1.8. **Not yet tested in game**. The test steps are at the end of
`docs/patch/v1-design.md`.
