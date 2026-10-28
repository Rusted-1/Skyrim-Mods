# v0.1.0: design

## What the patch is
`Music_Mods_Combined.esp`: an ESL-flagged ESP that overrides **47 music types** (`MUSC`) and
nothing else. It adds no new records and copies no audio. It only rewrites each music type's track
list (`TNAM`), so every mod's tracks and the vanilla tracks sit in one list.

Masters (11): Skyrim.esm, Update.esm, Dawnguard.esm, Melodies of Civilization.esp, Dawnguard Music
Overhaul.esp, Dawnguard Music Overhaul - Combat.esp, Still.esp, Celtic_Music_In_Skyrim_SE.esp,
Celtic_Music_In_Skyrim_SE_Dungeon.esp, NyghtfallMM_NonReplacer.esp, Chapter II - Soundtrack mod by
Dreyma Music.esp.

The two Chapter II v3.5 patches are **not** masters. The merge reads them as inputs, but their
only own records are the three dead tracks (see `../analysis/03-problems-found.md`), so the patch
replaces them and they should be disabled.

## Merge rules (implemented in `tools/build_combined.py`)
1. Every plugin that edits a music type gives an opinion: how many times each track appears.
2. The vanilla list from Skyrim.esm / Update.esm / Dawnguard.esm is an opinion too, so vanilla
   tracks a mod removed come back.
3. **A compatibility patch replaces the mod it patches.** If plugin P has plugin Q as a master and
   both edit the same music type, Q's opinion is replaced by P's, except for Q's own new tracks,
   which are always kept. This keeps the Still×ChII patch's deliberate trimming of Chapter II's
   silence weighting, and keeps `ChapterII_FarandBeyond` even though the old patch dropped it.
4. Each track appears as many times as its most generous opinion has it.
5. Null entries and the tracks in `tools/exclude_tracks.txt` are dropped.
6. Flags, priority and fade times come from the last plugin in load order. The mods never
   disagree with each other on these. The only differences are against vanilla, where
   Nyghtfall raises priority from 3 to 100 on two music types and lengthens the fade from 9 to 11 seconds on three. The patch
   keeps Nyghtfall's values. Details are in `v1-merge-report.md`.
7. Track records are never edited, so each mod's own day/night, eclipse, location and combat
   conditions still apply.

## Effect on silence
Vanilla exploration lists are roughly 30% silence entries. The merged lists are roughly 13%.

| Music type | Vanilla: entries / silence | Merged: entries / silence |
|---|---|---|
| MUSExploreForestFall | 35 / 10 (28%) | 138 / 15 (10%) |
| MUSExploreForestPine | 41 / 12 (29%) | 139 / 19 (13%) |
| MUSExploreSnow | 41 / 14 (34%) | 132 / 17 (12%) |
| MUSExploreReach | 41 / 14 (34%) | 123 / 17 (13%) |
| MUSExploreTundra | 42 / 13 (30%) | 134 / 15 (11%) |
| MUSExploreMountain | 46 / 15 (32%) | 118 / 20 (16%) |
| MUSExploreCoast | 37 / 10 (27%) | 111 / 16 (14%) |
| MUSExploreMarsh | 40 / 13 (32%) | 111 / 17 (15%) |

Not changed in v0.1.0. A later version could add silence entries back to a chosen ratio.

## Verification
`tools/verify.py` re-reads the built plugin and checks: ESL flag; masters present and in load
order; HEDR record count; every overridden music type exists in a source plugin; every track
reference resolves to a real track record, including vanilla ones; no null entries; no excluded
track listed; and no track that any source plugin put in a music type is missing. v0.1.0 passes
with 1,439 track entries across 47 music types. Rebuilding gives a byte-identical file.

Not yet tested in game.
