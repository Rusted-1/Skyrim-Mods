# Problems found in the source mods

1. **Load-order overwrites (the main problem).** 20 music types are edited by two to five mods,
   and only the last-loaded mod's list survives. See `01-plugins-and-conflicts.md`.

2. **Nyghtfall drops vanilla tracks, even in its "With Vanilla Music" package.**
   `NyghtfallMM_NonReplacer.esp` doesn't replace vanilla *files*, but in 28 music types it drops
   vanilla *tracks* from the lists. Examples:
   - `MUSCombat`: only Nyghtfall's 18 tracks. All 8 vanilla combat tracks are gone, and
     `MUSCombatCivilWar` loses both of its tracks
   - `MUSReveal`, `MUSReward`, `MUSStinger`, `MUSDread`, `MUSDiscoveryDungeon`,
     `MUSDiscoveryGeneric`, `MUSDungeonCleared`: every vanilla track removed
   - `MUSSpecialDeath`: 7 vanilla "failure" stings removed
   - Exploration lists: 2 to 15 vanilla tracks removed per region (e.g. 15 in `MUSExploreMountain`)

   The patch puts them all back.

3. **The two Chapter II patches are for v3.5 and are stale with v4.**
   `MelOfCiv - Chapter II Patch.esp` and `Still - Chapter II Patch.esp` add three tracks
   (`MUSStarlightMoC`, `MUSStarlightStill`, `MUSTouchTheSky`) that play `music\Starlight.xwm` and
   `music\Touch The Sky.xwm`. Chapter II v4 ships those files as
   `music\ChapterII_Starlight.xwm` and `music\ChapterII_TouchTheSky.xwm`, so these three tracks
   point at files that don't exist. Chapter II v4's own `ChapterII_Starlight` and
   `ChapterII_TouchtheSky` tracks are already in every list the old patches touch. The
   Still patch also omits `ChapterII_FarandBeyond` from `MUSExploreCoast`, most likely because the
   track was added after 3.5. All of their FormIDs still resolve in v4, so they don't crash; they
   are just partly out of date.

4. **Null entry in a Nyghtfall palette.** Nyghtfall's `NYGeneralDayPalette` track lists
   `00000000` as one of its sub-tracks. That's inside Nyghtfall's own track record, which the
   patch doesn't edit. It's probably harmless (the engine skips it), but if a day palette ever
   cuts out early, this is the first suspect.
