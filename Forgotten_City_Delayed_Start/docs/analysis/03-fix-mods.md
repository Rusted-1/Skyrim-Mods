# 03 – The Forgotten City fix mods and what they touch

The patch needs to work alongside these. None of them is included in this repo.

| Mod | Plugin | Records it edits | Overlap with this patch |
|---|---|---|---|
| The Forgotten City Entrance Extended 1.0.5 (Nexus 25730) | `tpos_forgottencity_entrance.esp` (masters Skyrim.esm, ForgottenCity.esp; not ESL) | Exterior cells around the ruins (Purewater Run area, 9 CELLs), 5 NAVMs + NAVI, 10 vanilla REFRs, 161 new REFRs, 7 Forgotten City statics in cell `00007665`, the Forgotten City map marker `0103478C`, one FC NAVM | None |
| The Forgotten City Music Bug Fixer 2.2 (Nexus 54019) | `Forgotten City Music Fix.esp` (ESL, master Skyrim.esm only) + `aaa_FCTriggerBoxScript` | 1 new trigger REFR in cell `00007665`, 1 MGEF, 1 SPEL. Its script removes the FC music types once `000FCQuest01` is completed | None |
| The Forgotten City - Good Ending Tweaks 1.1.0 (Nexus 77965) | `ForgottenCity - Good Ending Tweaks.esp` (ESL) + BSA with 4 scripts (sources included) | 11 FC NPCs incl. Cassia, 4 FC packages, 8 FC cells, 70 FC REFRs, dialogue, a Dragonborn temple cell, new quest `000FCAsdasfaTweaksQuest` (not start-game enabled; acts only after the ending) | None |

None of them edits SMQN `000FCBeginQuest`, QUST `000FCQuestStart` or `000FCQuest01`, or the
courier quest. This patch edits only `000FCBeginQuest` and adds one new global, so load order
between it and these three doesn't matter.
