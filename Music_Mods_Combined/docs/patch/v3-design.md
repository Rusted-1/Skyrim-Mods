# v0.3.0: add Around the Fire (tavern music)

## New input
`Around the Fire.esp` (Around the Fire 1.0, Nexus 36144). ESL-flagged, master Skyrim.esm only.
It adds five track records with no conditions, playing `music\Around the Fire\SE2020Tavern01–05.xwm`,
and edits two music types:

| Music type | Around the Fire's list |
|---|---|
| `MUSTavern` | vanilla `MUSTavern01`/`MUSTavern02` entries + SE2020 Tavern 01, 02, 05 |
| `MUSTavernB` | the three vanilla entries + SE2020 Tavern 03, 04 |

It keeps every vanilla track, so it removes nothing. Nyghtfall edits the same two music types, so
without this patch whichever of the two loads last would silence the other.

## Changes
- `tools/load_order.txt`: `Around the Fire.esp` added after the Celtic plugins and before Nyghtfall.
  Placing it before Nyghtfall keeps Nyghtfall's settings as the header source for the two tavern
  types. Around the Fire's settings are identical anyway.
- Masters go from 11 to 12.

## Effect on the built plugin
| Music type | v0.2.0 | v0.3.0 |
|---|---|---|
| `MUSTavern` | 6 entries | 9 entries (+3 Around the Fire) |
| `MUSTavernB` | 8 entries | 10 entries (+2 Around the Fire) |

No other music type changes. 1,579 track entries in all; `tools/verify.py` passes. The full list
comparison is in `v3-merge-report.md`. Its music folder (`music\Around the Fire\`) doesn't collide
with any other mod's files. Not yet tested in game.
