# 01 – How The Forgotten City starts

Source: `ForgottenCity.esp` from *The Forgotten City* 1.8 (SE, Nexus 1179), 1,797,101 bytes, and the
461 compiled scripts in `forgottencity.bsa`. The plugin was read with `tools/esp.py`, the BSA with
`tools/bsa.py`, and the scripts were disassembled with `tools/pexdis.py`. The mod ships no script
sources.

There are three ways the main quest `000FCQuest01` (`01012BB9`) can begin.

## 1. The courier letter (the only route this patch changes)

| Record | FormID | What it does |
|---|---|---|
| SMQN `000FCBeginQuest` | `010DCA3A` | Story Manager quest node under Skyrim.esm's **Change Location** event branch (`0001320E`), placed after sibling `000CBA9F`. Flags: Shares Event. Starts 1 quest. |
| QUST `000FCQuestStart` | `010DCFA3` | Event `CLOC`, not start-game enabled. Its stage 0 fragment (`QF__010DCFA3`) places the book *Cassia's Plea* in alias `Note` and calls `WICourierScript.addAliasToContainer`, so the vanilla courier delivers it. |

Conditions on `000FCBeginQuest` (all must pass):

```
GetQuestRunning(000FCQuest01)    == 0
GetQuestCompleted(000FCQuest01)  == 0
GetLevel  [run on PlayerRef]     >= 5
```

The node is evaluated each time the player changes location, so the courier is queued on the
first location change at level 5 or above. Nothing else in the plugin or its scripts references
`000FCQuestStart` or this node, so this node is the only way the courier gets sent.

Reading the letter (`FCReadNote01`, `OnContainerChanged`) sets `000FCQuest01` to stage 20.

## 2. Meeting Cassia in the ruins

Cassia (`000FCCassia`, ACHR `0101096F`) stands inside `000ForgottenCity01`. Her greeting INFO
`010AE7B3` ("That's close enough – who are you?") runs `TIF__010AE7B3`, which calls
`FCQuest01.Start()`. The ruins entrance door (`01034783`) and map marker (`0103478C`) are in the
world from the start of a new game.

## 3. The auto-door trigger

`FCAutoOpenDoorScript` (on REFR `010499C5`, base `000FCAutoDoorOpen`, inside
`000ForgottenCity01`) calls `FCQuest01.Start()` on trigger enter if the quest is neither running
nor completed.

Routes 2 and 3 are deliberately left alone: only the courier is delayed.
