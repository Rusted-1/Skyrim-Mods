# v0.2.0: drop the Chapter II v3.5 patches as inputs

## Why
`MelOfCiv - Chapter II Patch.esp` and `Still - Chapter II Patch.esp` were only in my mods folder
for reference. They aren't part of my load order. v0.1.0 read them as inputs anyway, and that
changed the result in two ways, both based on Chapter II **v3.5** instead of the v4 I actually run.

## Changes
- `tools/load_order.txt`: the two Chapter II patches removed. The merge now reads only the eight
  music mods and the three game masters.
- `tools/exclude_tracks.txt`: emptied. Its three entries were tracks from those patches.
- Merge rule 3 (a patch replaces the mod it patches) is still in the tool, but nothing in the
  current load order triggers it.

## Effect on the built plugin
Same 47 music types and 11 masters. Track entries go from 1,439 to 1,574. Every check in
`tools/verify.py` passes.

1. **Chapter II tracks now sit where v4 puts them.** The v3.5 patches carried old placements:
   - `ChapterII_StoriesofOld` was in all eight regional exploration lists. v4 lists it only in
     `MUSTownTest`, so it's now town-only.
   - `ChapterII_Winterhold` and `ChapterII_TheHeartlands` were in `MUSTownTest`. v4 lists them only
     in the exploration lists, so they're now exploration-only.
   All three are daytime tracks (08:00–18:00).

2. **More silence between tracks.** The Still patch had cut Chapter II's extra silence entries back
   to Still's level. Without it, Chapter II's own silence weighting stands.

| Music type | Vanilla: entries / silence | v0.1.0 | v0.2.0 |
|---|---|---|---|
| MUSExploreForestFall | 35 / 10 (28%) | 138 / 15 (10%) | 154 / 32 (20%) |
| MUSExploreForestPine | 41 / 12 (29%) | 139 / 19 (13%) | 155 / 36 (23%) |
| MUSExploreSnow | 41 / 14 (34%) | 132 / 17 (12%) | 149 / 35 (23%) |
| MUSExploreReach | 41 / 14 (34%) | 123 / 17 (13%) | 140 / 35 (25%) |
| MUSExploreTundra | 42 / 13 (30%) | 134 / 15 (11%) | 151 / 33 (21%) |
| MUSExploreMountain | 46 / 15 (32%) | 118 / 20 (16%) | 135 / 38 (28%) |
| MUSExploreCoast | 37 / 10 (27%) | 111 / 16 (14%) | 131 / 37 (28%) |
| MUSExploreMarsh | 40 / 13 (32%) | 111 / 17 (15%) | 128 / 35 (27%) |

Exploration now has about 20–28% silence, against vanilla's 27–34%.

Everything else in `v1-design.md` still applies. The full list comparison is in
`v2-merge-report.md`. Not yet tested in game.
