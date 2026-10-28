# v0.4.0: share rules (who plays where, and when)

v0.1.0–v0.3.0 only merged the lists, so a mod's share of the music depended on how many tracks it
happened to have (Celtic Music, with 37 tracks in some regions, dominated). v0.4.0 weights every
list to hit shares I chose.

## The rules
From my rules list (`Music_Overhaul\Music_Mods_Used\1. Nyghtfall should only play at ni.txt`),
with these decisions:

| Decision | Choice |
|---|---|
| How strong "priority" is | The lead mod gets **75%** of the music picks |
| Nyghtfall outside combat | **Leads at night** (20:00–06:00); **5%** by day |
| Night in a place with its own lead mod | **The place's mod wins**; Nyghtfall stays at 5% there |
| Vanilla | **10%** of the music wherever mods add music |

Applied per music type (classes are set in `tools/rules.json`):

| Class | Music types | Lead | Vanilla | Nyghtfall | Everyone else |
|---|---|---|---|---|---|
| town | `MUSTownTest`, `MUSCastle` | Melodies of Civilization 75% | 10% | 5% | share 10% equally |
| tavern | `MUSTavern`, `MUSTavernB` | Around the Fire 75% (+10% nobody else claims = 85%) | 10% | 5% | none |
| dawnguard | Soul Cairn, Forgotten Vale, Castle Volkihar, Dawnguard bosses | Dawnguard Music Overhaul 75% | 10% | 5% | share 10% equally |
| combat | `MUSCombat`, boss, Civil War, Helgen, Umbra | Nyghtfall 75% (+15% = 90%) | 10% | (lead) | none |
| default, night | everything else: wilderness, dungeons, Sovngarde, discoveries and other moments | Nyghtfall 75% | 10% | (lead) | Chapter II, Celtic, Still share 15% equally |
| default, day | same | none | 10% | 5% | Chapter II, Celtic, Still share 85% equally |

Rules that apply throughout:
- A share nobody present can take goes to the lead. With no lead, it goes to vanilla. That's why
  stingers are 95% vanilla by day: nothing else but Nyghtfall has stingers.
- Pauses keep the share they had in v0.3.0, and the shares above are of the music picks.
- Rule 4 ("same chance where they're present together") is per mod, not per track: Still's 5
  tracks get as much play in total as Celtic's 37.

## How it's done
The game picks from a list at random, so a track listed n times is n times as likely.
`tools/weighting.py` works out how many times to list each track:

1. Each track's own time window is read from its conditions (`GetCurrentTime`), per half hour.
2. For each half hour, the rules give a target share per mod.
3. A fixed list can't give different shares by day and by night. So for each group whose share has
   to change with the clock, every track is replaced by **two copies**, and day copies and night
   copies get separate weights. The groups are Nyghtfall, vanilla and pauses where Nyghtfall leads
   at night, plus any mod whose number of eligible tracks changes between day and night, such as
   Chapter II's day and night palettes.
   - A copy is a new track record in `Music_Mods_Combined.esp`, EditorID
     `MMC_<D|N>_<load order index>_<source object id>`.
   - It plays the same music file with the same cues, sub-tracks and conditions. A time check is put
     **in front** of those conditions: night is `time >= 20 OR time < 6`, day is
     `time >= 6 AND time < 20`.
   - The check goes in front because 52 source tracks end on an OR condition, which would
     otherwise join a check added at the end.
   - No audio is copied. 385 copies, well inside the ESL limit of 2,048 new records.
4. A solver picks a weight per group (and per period for split groups). Rounding to whole entries
   then uses the scale with the lowest share error, with a small penalty on list length.
   - Exact ratios need long lists: one Still track must count about 7 times as much as one Celtic
     track. The longest list is 2,277 entries (about 9 KB). The whole plugin is 212 KB.

Copying also made one bug fix possible (`tools/fixes.txt`). Nyghtfall's **Choir**
(`NYExploreSpecialC`) is meant for late at night, but its conditions read
`time >= 22 AND time <= 5`, which is never true, so it never played. Its copies use
`>= 22 OR <= 5`, like Nyghtfall's other late-night tracks.

## Results (from `v4-merge-report.md`, shares of all picks including pauses)

| Music type | Day | Night |
|---|---|---|
| Reach (wilderness) | pause 28%, Celtic 22%, Chapter II 20%, Still 18%, vanilla 9%, Nyghtfall 4% | Nyghtfall 55%, pause 26%, vanilla 7%, Celtic 4%, Still 4%, Chapter II 4% |
| Towns and cities | Melodies of Civilization 67%, pause 13%, vanilla 9%, Nyghtfall 5%, Celtic 4%, Chapter II 3% | Melodies of Civilization 61%, pause 13%, Celtic 9%, vanilla 9%, Nyghtfall 5%, Chapter II 4% |
| Taverns | Around the Fire 85%, vanilla 10%, Nyghtfall 5% | same |
| Soul Cairn | Dawnguard Music Overhaul 48%, pause 44%, vanilla 6%, Nyghtfall 3% | about the same |
| Dungeons | Celtic 43%, Still 43%, vanilla 10%, Nyghtfall 5% | Nyghtfall 75%, vanilla 10%, Celtic 8%, Still 8% |
| Combat | Nyghtfall 90%, vanilla 10% | same |

With pauses taken out, the wilderness lists land within a few points of the targets. Two things
account for most of the remaining gap:
- **Tracks with their own hours.** Chapter II's single tracks only run 08:00–18:00, and Nyghtfall's
  evening tracks run 18:00–22:30, so the mix moves around within each period.
- **Long pauses.** The Soul Cairn, Forgotten Vale and Castle Volkihar lists were already 30–50%
  pauses in vanilla, and the patch keeps that.

`tools/verify.py` recounts the shares independently from the built plugin. The worst average gap
is 7.7% (`MUSExploreForestFall`), and it fails the build above 12%.

## Things only an in-game test can confirm
- That the engine weights repeated list entries as expected. Vanilla relies on this for its
  silence entries, so it should.
- That lists of about 2,000 entries cause no hitches when the music type changes.
- Conditions other than time (place, dragon, eclipse) are treated as met when computing shares.
  For example, Nyghtfall's Whiterun-only town track counts toward its 5% everywhere, but only plays
  in Whiterun.

To change any percentage, edit `tools/rules.json` and rebuild.
