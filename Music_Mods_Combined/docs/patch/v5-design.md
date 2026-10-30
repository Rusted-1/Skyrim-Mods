# v0.5.0: LORKHAN - Soundtrack Replacer as an equal partner

## What LORKHAN is
LORKHAN - Soundtrack Replacer (Nexus 115096, 1.0) is a **file replacer**. Its FOMOD has 42 options
(`1. Main Menu` to `42. Valhalla Combat`) that install new audio under the **vanilla** file
names, e.g. `music\explore\reach\palette\day\a\mus_palette_reach_day_a_01.xwm`.
- It adds no music records. Its only plugin, `LORKHAN - UI Sound Effects.esp`, is for menu sounds
  (option 34), and options 32–42 replace sound effects rather than music.
- 224 of its 249 music files match the file of a vanilla track record, covering **233 vanilla
  track records**. The match ignores the extension: vanilla records name `.wav`, and the game
  plays the `.xwm`.
- The 25 that don't match are the main menu theme, Dragonborn/Solstheim music, combat "finale"
  endings and one snow palette part. No mod in this set touches those, so they play as LORKHAN
  intends.
- No file of LORKHAN's collides with any other mod in the set. Every other mod keeps its music
  in its own folder, including the Celtic BSAs and Nyghtfall's NonReplacer version.

So with LORKHAN installed, every vanilla entry in the patch's lists plays LORKHAN's music.

## Decisions
| Question | Choice |
|---|---|
| How much LORKHAN should play | **Equal partner**: a 4th equal share with Chapter II, Celtic and Still where those share (wilderness, dungeons, Sovngarde, moments). It keeps the 10% slot in towns, taverns, combat and Dawnguard areas |
| Vanilla tracks LORKHAN doesn't cover | Kept, counted in the same group |

## Changes
- `tools/rules.json`:
  - `vanilla_group` renames the vanilla group to `LORKHAN / vanilla`.
  - `vanilla_peer_classes: ["default"]` makes that group an equal partner in the default class,
    instead of a fixed 10%.
- `tools/weighting.py`: reads those two settings instead of a hard-coded "Vanilla".
- No new masters. LORKHAN has no music plugin, and the patch doesn't need it installed. Without it,
  the `LORKHAN / vanilla` share is simply the original vanilla music.

## Effect (shares of all picks including pauses, from `v5-merge-report.md`)
| Music type | Day | Night |
|---|---|---|
| Reach (wilderness) | pause 27%, Celtic 21%, LORKHAN/vanilla 17%, Chapter II 16%, Still 15%, Nyghtfall 3% | Nyghtfall 54%, pause 26%, Celtic 6%, LORKHAN/vanilla 5%, Chapter II 4%, Still 4% |
| Dungeons | Celtic 32%, LORKHAN/vanilla 32%, Still 32%, Nyghtfall 5% | Nyghtfall 75%, Celtic 8%, LORKHAN/vanilla 8%, Still 8% |
| Stingers, deaths and other moments | LORKHAN/vanilla 95%, Nyghtfall 5% | Nyghtfall 75%, LORKHAN/vanilla 25% |
| Towns | unchanged: Melodies of Civilization 67%, LORKHAN/vanilla 9% … | unchanged |
| Combat | Nyghtfall 90%, LORKHAN/vanilla 10% | same |

The independent recount in `tools/verify.py` passes. The worst average gap is 6.7%
(`MUSExploreReach`), down from 7.7% in v0.4.0. List entries total 23,265 (was 28,613).
Not yet tested in game.

## Which LORKHAN options matter
Only the music options (1–31) affect what plays. If you skip one, those tracks fall back to
vanilla audio. The shares stay the same either way. Options 32–42 are sound effects and have
nothing to do with this patch.
