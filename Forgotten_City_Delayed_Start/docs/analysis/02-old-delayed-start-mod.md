# 02 – The old "Forgotten City - Delayed Start" mod (Nexus 56958)

Archive `The Forgotten City - Delayed Start-56958-1-00-1634018441.zip` holds one file:
`Data/Forgotten City - Delayed Start.esp`, 372 bytes. Not included in this repo (another author's
plugin).

## What it contains

| Field | Value |
|---|---|
| Header flags | `0x200` (ESL) |
| HEDR | version 1.70, 2 records, next object ID 0x800 |
| Masters | Skyrim.esm, ForgottenCity.esp |
| Records | one: an override of SMQN `000FCBeginQuest` (`010DCA3A`) |

The override is a byte-for-byte copy of the original node except one value: the level condition
`GetLevel(PlayerRef) >= 5.0` becomes `>= 25.0`. Parent, sibling, flags, the two quest-state
conditions and the quest to start are all unchanged.

## Why it didn't work

In testing, the courier still arrived at level 5 with this plugin installed. The file itself looks
valid: the record is well-formed, the FormID and parent and sibling nodes match Forgotten City 1.8,
and the condition layout is correct. No other record or script in Forgotten City can start the
courier (see `01-how-the-quest-starts.md`), and none of the three fix mods edits this node (see
`03-fix-mods.md`). So the override most likely never took effect in the game. A few possible
causes, none of which can be confirmed from the files alone:

* the plugin wasn't enabled or loaded (light plugins are easy to miss in Vortex's plugin list);
* the save had already queued the courier before the plugin was added (`000FCQuestStart`
  never stops once it runs, so the letter is already on its way);
* something in that load order outside these files changed the Change Location branch.

This patch doesn't rely on the old file. It rebuilds the override from the original record and
adds a global that can be read from the console (`GetGlobalValue FCDS_CourierLevel`). That gives a
one-line check that the patch really loaded. See `docs/patch/v1-design.md`.
