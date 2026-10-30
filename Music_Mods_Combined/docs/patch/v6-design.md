# v0.6.0: short pauses, LORKHAN as a true equal, steadier shares

Asked for: "very little downtime between songs" and "LORKHAN will play as often as Celtic,
Chapter II, etc."

## 1. Pauses: 5% of picks, 10 seconds each
Before, pauses were the vanilla silence entries a list happened to have (10, 15 or 30 seconds),
at 20–30% of picks and up to 41% at dawn. In wilderness by day that came to roughly one silence for
every three pieces of music.

Now (`rules.json` "pause"), wherever a list had silences, they're all replaced by vanilla's
**`_MUSExploreSILENT10`**: 10 seconds, allowed at every hour (an Update.esm record). It makes up
**5% of picks** at all hours. With music pieces running a few minutes each, that's a short breath
about once every 20 picks. Lists that never had silences (combat, taverns, stingers) still have
none.

## 2. LORKHAN gets its own group
In v0.5.0 the equal share went to "LORKHAN / vanilla". About a quarter of that group's weight was
vanilla tracks LORKHAN doesn't replace, so LORKHAN's own music came out a few points below the
others (14% against Celtic's 19% at midday in the Reach).

Now:
- `tools/lorkhan_files.txt` lists the 249 music files LORKHAN installs. It holds file names only,
  no audio, and was made with `tools/list_replacer.py` from the LORKHAN archive.
- A vanilla track whose file is on that list, or a vanilla palette with at least half its parts on
  it, is in the **LORKHAN** group.
- LORKHAN is the equal partner (`vanilla_group: "LORKHAN"`). In the wilderness, dungeons and
  moments it shares equally with Chapter II, Celtic and Still. Elsewhere it holds the 10% slot.
- Vanilla tracks LORKHAN doesn't replace form a group **Vanilla** with a token **2%**
  (`shares.vanilla_leftover`).

## 3. Five clock periods instead of day/night
Tracks start and stop at many hours: 05:00, 05:30, 08:00, 18:00, 18:30, 22:00, 22:30. With only
day and night copies, shares drifted within each half. For example, Nyghtfall was at 72% at 20:00
but 47% at midnight, and the 06:00–08:00 window was 41% silence.

Copies are now made per period (`rules.json` "periods"):

| Period | Hours |
|---|---|
| A | 20:00–22:00 |
| B | 22:00–06:00 |
| C | 06:00–08:00 |
| D | 08:00–18:00 |
| E | 18:00–20:00 |

Night, when Nyghtfall leads, is still 20:00–06:00 (A+B).
- A group is split per period if its share changes with the clock (Nyghtfall, LORKHAN, Vanilla
  and pauses in the default class), or if its number of eligible tracks changes during the day.
- **851 copies** (EditorIDs `MMC_<A–E>_<load order index>_<object id>`). That's under half the
  ESL limit of 2,048 new records.
- List length is now a hard cap of 2,500 entries per music type (the longest is 2,493). The
  plugin is 349 KB.

## Results (hour-by-hour check of the built plugin; daily averages in `v6-merge-report.md`)
| Music type, hour | Mix (all picks, pauses included) |
|---|---|
| Reach 10:00 | Celtic 23%, Still 22%, LORKHAN 22%, Chapter II 22%, pause 5%, Nyghtfall 4%, vanilla 2% |
| Reach 07:00 (Still has no tracks before 08:00) | Celtic 31%, Chapter II 30%, LORKHAN 28%, pause 5%, Nyghtfall 4%, vanilla 2% |
| Reach 01:00 | Nyghtfall 68%, Celtic 7%, Still 6%, LORKHAN 6%, Chapter II 6%, pause 6%, vanilla 2% |
| Dungeons, day | LORKHAN 32%, Still 32%, Celtic 32%, Nyghtfall 5% |
| Dungeons, night | Nyghtfall 75%, LORKHAN 8%, Still 8%, Celtic 8% |
| Towns 10:00 | Melodies of Civilization 72%, LORKHAN 9%, Celtic 5%, Nyghtfall 5%, pause 4%, Chapter II 4%, vanilla 2% |
| Taverns | Around the Fire 85%, LORKHAN 10%, Nyghtfall 5% |

Averaged over a whole day period, Still reads a few points lower in the wilderness (e.g. 18%
against Celtic's 26% in the Reach). That's because Still has no tracks from 06:00 to 08:00; hour by
hour the four are level. The independent recount in `tools/verify.py` passes: the worst average gap
is 5.2% (`MUSExploreMarsh`), and pauses are checked against the 5% target too. Not yet tested
in game.

## If LORKHAN isn't installed
The patch still works. The LORKHAN share is then just vanilla audio from those same tracks.
