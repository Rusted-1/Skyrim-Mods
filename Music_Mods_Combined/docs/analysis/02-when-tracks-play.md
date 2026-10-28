# How the mods decide when a track plays (location, time of day, events)

The project goal is music that fits **where you are, the time of day, and what is happening**.
These mods already do most of that themselves, through conditions on their own track records
(`MUST`). The patch never edits track records, so all of it keeps working.

| Mod | Conditions found on its tracks | Effect |
|---|---|---|
| Chapter II | `GetCurrentTime` on 64 checks; 27 checks against a Dawnguard record (function code 149, apparently the eclipse state) | 20 "palette" tracks split into Day (06:00–20:00) and Night (20:00–06:00) sets per region (Forest, Mountain, Reach, Snow, Tundra, Sovngarde), plus `…ECLIPSE` night palettes for the Dawnguard vampire eclipse. Single tracks like `ChapterII_Starlight` (22:00–05:00) and `ChapterII_TouchtheSky` (08:00–18:00) carry their own time windows |
| Nyghtfall | `GetCurrentTime` on 92 checks; three other functions (codes 707, 359, 310) on 39 | Explore tracks split into Morning / Day / Evening / Night folders by clock time; combat tracks tell dragon fights from NPC fights; town tracks check the worldspace or location (e.g. a Whiterun-only town piece); some special tracks check location type |
| Still | `GetCurrentTime` on 5 of its 6 tracks | Day (08:00–18:30), Dusk (18:00–22:30) and Night (22:00–05:30) pieces |
| Melodies of Civilization | `GetCurrentTime` on 6 of its 8 tracks | Town Day pieces (05:00–22:30) and Town Night pieces (22:00–05:30) |
| Dawnguard Music Overhaul | non-time conditions on 8 tracks | Castle and boss tracks with place or side checks (e.g. Imperial vs Stormcloak castle music, Dawnguard vs vampire boss music) |
| Celtic, Around the Fire | none | Plain tracks: play any time their music type is active |
| Vanilla | not inspected | Has day/night and eclipse palettes by name (`MUSExploreReachDayPalette`, `…NightPaletteECLIPSE01`) |

Location is handled by **which music type a track is in** (a Reach track is only in
`MUSExploreReach`), and the patch keeps every track in the music types its author chose.

So after the patch, when you are in the Reach at night, the game's pick comes from Nyghtfall's
night tracks, Chapter II's night palette, vanilla's night palette, and the unconditioned
Celtic / Still tracks. Day-only tracks stay silent at night.

Condition function codes 149, 707, 359 and 310 are recorded by number; I haven't looked up their
names yet. The parameters show they test a keyword, a location type and a worldspace.

## Things to consider for the in-depth phase
(v0.4.0 addressed the share balance: see `docs/patch/v4-design.md`. The points below are kept for history.
Correction: an earlier version of this file said Still and Melodies of Civilization had no time
conditions; both do, as the table above now shows.)

- **Silence.** With every mod's tracks in one list, silence entries are about 20–28% of the
  exploration lists, against vanilla's 27–34% (see `docs/patch/v2-design.md`). Silence weight
  can be tuned later if music feels too constant.
- **Unconditioned tracks.** Celtic, Still, MoC, DMO and Around the Fire tracks play at any hour. Giving some of
  them day/night windows would need new track records of my own that point at their music
  files, which is possible without copying any audio.
- **Combat.** Combat music is now Nyghtfall's 18 tracks plus vanilla's 8. Nothing else in the
  set adds combat music, except the Dawnguard boss module.
