# v1.0.0 design: Glad You're Here patch

## Goal
Each hug from Glad You're Here lowers Stress and Fear's stress. The amount depends on who you hug:
children 25, spouse/lover 18, good friend 10, anyone else 5.

## Plugin: `MoreStressGone_GladYoureHere.esp`
* ESL-flagged, HEDR 1.71, form version 44. Only master: Skyrim.esm (for `PlayerRef`).
* Built from scratch by `tools/MsgTool` (`build`). No Creation Kit or other author's file is used.

| Record | FormID | Contents |
|---|---|---|
| QUST `MoreStressGone_GYHQuest` (new) | `xx000800` (light range) | Start Game Enabled + Run Once, priority 50. Alias 0 "Player", forced ref `PlayerRef` (00000014), with `MoreStressGone_GYHScript` attached. No quest scripts, no stages. |

## Script: `MoreStressGone_GYHScript` (extends ReferenceAlias)
* `OnInit` and `OnPlayerLoadGame` call `RegisterForModEvent("GYH_OnHugActor", "OnGYHHug")`.
  Re-registering on every load keeps it working whether or not SKSE saved the registration.
* `OnGYHHug(Form akSender, Form akTarget)`:
  1. Finds `Stress_Total` with `Game.GetFormFromFile(0x801, "Stress and Fear.esp")`. If it's missing
     or the target isn't an actor, it does nothing.
  2. Works out the amount:

     | Test (first match wins) | Amount |
     |---|---|
     | sender is `WW42HugFamily` (`GetFormFromFile(0x21AF77, "ImGladYoureHere.esp")`) and target `IsChild()` | 25 |
     | sender is `WW42HugFamily`, or target's relationship rank to the player is 4 (Lover) | 18 |
     | relationship rank 2 (Confidant) or 3 (Ally) | 10 |
     | anyone else (followers, friends at rank 1, strangers, pets) | 5 |

  3. `Stress_Total = max(0, Stress_Total - amount)`, the same clamp Stress and Fear uses.
  4. If stress actually dropped, it shows `Debug.Notification("The hug eases your stress.")`.

A spouse hugged through the follower quest (for example, when they're also your follower) still
gets 18, because marriage gives relationship rank 4.

### How the `.pex` is made
There's no Papyrus compiler on the build PC. `Build.cs` `GYHScript()` assembles the compiled script
one instruction at a time, mirroring `MoreStressGone_GYHScript.psc`. It follows the same patterns
the real compiler used in Stress and Fear's and GYH's scripts (studied with `MsgTool dis`):
`::temp` locals, `::NoneVar`, relative jumps, every argument passed explicitly, and the generated
`GetState`/`GotoState` functions. Before writing, `Check()` confirms that every identifier is
declared and that every jump stays inside its function.

## Verification (file level)
`MsgTool verify` runs 17 checks, and all of them pass:
* both files are byte-identical to a fresh build, and the script reads back cleanly;
* ESL flag, only master Skyrim.esm, a single QUST record in the light range, Start Game Enabled,
  alias forced to the player, script attached, alias entry points at the quest;
* form 0x801 in Stress and Fear.esp is GLOB `Stress_Total`, and form 0x21AF77 in ImGladYoureHere.esp
  is QUST `WW42HugFamily`;
* `WW42HugFamily` has `NPC = 1`, and `WW42HugFollower` has `NPC = 0`;
* GYH's compiled `HugActor` creates `GYH_OnHugActor` and pushes `(self, akTarget)`.

The tool's `.pex` reader and writer were also tested by round-tripping every compiled script in
both mods (all byte-identical).

## In-game test (pending)
1. Install with Glad You're Here and Stress and Fear enabled. Load a save.
2. Console: `GetQuestRunning MoreStressGone_GYHQuest` → 1.
3. `set Stress_Total to 60`, then hug each type of person and check `GetGlobalValue Stress_Total`
   after each hug (set it back to 60 between them):
   * a follower or a stranger you've just met → 55
   * a friend raised to Confidant/Ally (`setrelationshiprank player 3` on them) → 50
   * your spouse → 42
   * an adopted child → 35
4. `set Stress_Total to 3`, hug anyone → 0 (never negative).
5. Save, quit to desktop, reload, hug again → it still works (re-registration on load).
6. Papyrus log (`bEnableLogging=1`): no errors mentioning `MoreStressGone_GYHScript`.

## Known limits
* Repeated hugs stack, with no cooldown. Ten hugs in a row with a child clear 250 stress.
* A hug whose animation was blocked by scene protection still counts (GYH still sends the event).
* Horse hugs don't lower stress (GYH doesn't send the event for them).
