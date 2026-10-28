# How the mods decide when a track plays (location, time of day, events)

The project goal is music that fits **where you are, the time of day, and what is happening**.
These mods already do most of that themselves, through conditions on their own track records
(`MUST`). The patch never edits track records, so all of it keeps working.

| Mod | Conditions found on its tracks | Effect |
|---|---|---|
| Chapter II | `GetCurrentTime` on 64 checks; 27 checks against a Dawnguard record (function code 149, apparently the eclipse state) | 20 "palette" tracks split into Day (06:00–20:00) and Night (20:00–06:00) sets per region (Forest, Mountain, Reach, Snow, Tundra, Sovngarde), plus `…ECLIPSE` night palettes for the Dawnguard vampire eclipse. Single tracks like `ChapterII_Starlight` (22:00–05:00) and `ChapterII_TouchtheSky` (08:00–18:00) carry their own time windows |
| Nyghtfall | `GetCurrentTime` on 92 checks; three other functions (codes 707, 359, 310) on 39 | Explore tracks split into Morning / Day / Evening / Night folders by clock time; combat tracks tell dragon fights from NPC fights; town tracks check the worldspace or location (e.g. a Whiterun-only town piece); some special tracks check location type |
| Celtic, Still, MoC, DMO | none | Plain tracks: play any time their music type is active |
| Vanilla | not inspected | Has day/night and eclipse palettes by name (`MUSExploreReachDayPalette`, `…NightPaletteECLIPSE01`) |

Location is handled by **which music type a track is in** (a Reach track is only in
`MUSExploreReach`), and the patch keeps every track in the music types its author chose.

So after the patch, when you are in the Reach at night, the game's pick comes from Nyghtfall's
night tracks, Chapter II's night palette, vanilla's night palette, and the unconditioned
Celtic / Still tracks. Day-only tracks stay silent at night.

Condition function codes 149, 707, 359 and 310 are recorded by number; I haven't looked up their
names yet. The parameters show they test a keyword, a location type and a worldspace.

## Things to consider for the in-depth phase
- **Less silence.** With every mod's tracks in one list, silence entries fall from about 30% of
  the vanilla exploration lists to about 13% (see `docs/patch/v1-design.md`). Music will play
  almost constantly while exploring. Adding silence weight is one option to tune later.
- **Unconditioned tracks.** Celtic, Still, MoC and DMO tracks play at any hour. Giving some of
  them day/night windows would need new track records of my own that point at their music
  files, which is possible without copying any audio.
- **Combat.** Combat music is now Nyghtfall's 18 tracks plus vanilla's 8. Nothing else in the
  set adds combat music, except the Dawnguard boss module.
