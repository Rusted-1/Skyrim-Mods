# v1.0.0 design

## Goal
Hold back The Forgotten City's courier letter until the player is level 25. Leave everything else,
including finding the ruins on your own, unchanged.

## Plugin: `ForgottenCity_DelayedStart.esp`
* ESL-flagged, HEDR 1.70, form version 44. Masters: Skyrim.esm, ForgottenCity.esp.
* Built from scratch by `tools/build_patch.py`; no Creation Kit or other author's file is used as
  a template. The override values are taken from the original record and listed in the script.

| Record | FormID | Change |
|---|---|---|
| GLOB `FCDS_CourierLevel` (new) | `xx000800` (light range) | short, value 25 |
| SMQN `000FCBeginQuest` (override) | `010DCA3A` | third condition is now `GetLevel [PlayerRef] >= FCDS_CourierLevel` (global comparison) instead of `>= 5.0`. Everything else matches the original. |

Using a global instead of a fixed 25 means:
* the level can be changed in game: `set FCDS_CourierLevel to 30`;
* `GetGlobalValue FCDS_CourierLevel` in the console proves the patch is loaded. If the console
  says the global doesn't exist, the plugin isn't active (the likely problem with the old mod).

## Verification (file level)
`tools/verify.py` compares the built plugin with ForgottenCity.esp 1.8. All 20 checks pass: ESL
flag, masters, header, record count, global ID/name/type/value, override target, identical
subrecord layout, unchanged quest-state conditions, parent/sibling/flags/quest, and the new
level condition (GetLevel, >=, uses global, global is FCDS_CourierLevel, run on PlayerRef).

## In-game test (pending)
1. New character (or a save below level 5 that has never been sent the letter). Make sure the
   old `Forgotten City - Delayed Start.esp` is not installed.
2. Console: `GetGlobalValue FCDS_CourierLevel` → should print 25.
3. `player.setlevel 10`, then walk into a city / change location a few times → no courier.
4. `player.setlevel 25`, change location → the courier should find you with Cassia's Plea within
   a few location changes (the vanilla courier can take a little while).

## Known limits
* A save where the courier was already sent keeps it; the patch can only stop the send.
* The Forgotten Ruins can still be found and entered on your own at any level (by design).
