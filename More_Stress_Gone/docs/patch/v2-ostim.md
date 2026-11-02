# v1.1.0 design: OStim patch

## Goal
Each time the player climaxes in an OStim scene, lower Stress and Fear's stress by an amount
for the act, plus a bonus for the partner. With Fertility Mode Reloaded, a conception involving the
player lowers it by another 10.

Decisions (from Rusted-1):
* The act is taken from the **finishing scene**: the scene playing when the player climaxes.
* **Every climax** counts, not once per scene.
* The FMR bonus comes **when FMR reports the conception** (usually later in game time), with its own message.
* The FMR bonus applies whether the player is the **father or the mother**.

## Plugin: `MoreStressGone_OStim.esp`
Same shape as the Glad You're Here plugin (see `v1-design.md`): ESL, HEDR 1.71, only master
Skyrim.esm, one QUST `MoreStressGone_OStimQuest` (`xx000800`, Start Game Enabled + Run Once,
priority 50), alias 0 forced to `PlayerRef` with `MoreStressGone_OStimScript` attached.

## Script: `MoreStressGone_OStimScript` (extends ReferenceAlias)
* `OnInit` / `OnPlayerLoadGame` → `RegisterEvents()`: `ostim_orgasm` → `OnOStimOrgasm`,
  `FMR_Conception` → `OnFMRConception`.
* `OnOStimOrgasm(String asEventName, String asSceneID, Float afIndex, Form akSender)`:
  1. Ignore it unless `akSender` is the player.
  2. Adults only: if any actor in thread 0 `IsChild()`, do nothing.
  3. Act tier. `OMetadata.FindActionSuperloadCSVv2(asSceneID, ParticipantPositionsAny = <player position>, …)`,
     checked highest first; the first match sets the amount:

     | Amount | Tag condition |
     |---|---|
     | 20 | all of `intercourse,vaginalpenetration` |
     | 15 | all of `intercourse,analpenetration` |
     | 10 | any of `oral`, blacklist `nipplestimulation` |
     | 5 | any of `sexual` |
     | 5 (no partner bonus) | nothing matched |

  4. Partner bonus. The partner is the other side of the matched action: its actor, or, if that's the
     player, its target. If that's still the player (solo act), there's no bonus. Otherwise, by the
     partner's relationship rank to the player: 4 (lover/spouse) → +10, 2–3 (confidant/ally) → +5.
  5. `LowerStress(amount, "You feel the tension drain away.")`.
* `OnFMRConception(Form akMother, Form akFather, Int aiTrackingIndex)`: if the mother is the player,
  or the father is the player (or the player's ActorBase), → `LowerStress(10, "You feel a deep sense of contentment.")`.
* `LowerStress(Int, String)`: finds `Stress_Total` with `GetFormFromFile(0x801, "Stress and Fear.esp")`, does nothing if
  it's missing, sets it to max(0, stress − amount), and shows the message if it dropped.

The `.pex` is assembled in `tools/MsgTool/OStimPatch.cs` the same way as the GYH one, including all
25 arguments of each `FindActionSuperloadCSVv2` call (Papyrus passes defaults explicitly). The
superload call and the `ostim_orgasm` parameters are the same ones OStim documents and uses itself.

### What each OStim 7.5c action counts as
From `MsgTool ostim-table` (built-in actions; add-on actions with the same tags behave the same):

| Amount | Actions |
|---|---|
| 20 | vaginalsex, vaginaltailsex |
| 15 | analsex, analtailsex |
| 10 | anallicking, blowjob, deepthroating, penilelicking, rimjob, testicularlicking, vulvaleating, vulvallicking |
| 5 | analfingering, analfisting, analtoying, boobjob, breastslapping, buttjob, cumonbutt, cumonchest, cumonvulva, facial, femalemasturbation, footjob, grindingfoot, grindingobject, grindingpenis, grindingthigh, gropingtesticles, handjob, lickingnipple, malemasturbation, oraltoying, rubbingpenisagainstface, suckingnipple, tailjob, thighjob, tribbing, vaginalfingering, vaginalfisting, vaginaltoying, vulvalrubbing |

Non-sexual actions (kissing, cuddling, …) never set the tier. A climax with only those in the scene
counts as 5 with no partner bonus.

Examples: vaginal with your spouse → 30; oral with a stranger → 10; vaginal with your spouse, and
FMR later reports a conception → 30 + 10 = 40.

## Tool changes in 1.1.0
* The tool now builds every patch from a table (`Build.Patches`). `build` takes the FOMOD package
  folder and writes each patch's folder.
* `verify` became `verify-gyh` (same 17 checks), and there's a new `verify-ostim`. The GYH plugin and script
  are byte-identical to 1.0.0 (checked with `cmp` against the 1.0.0 build).

## Verification (file level)
`MsgTool verify-ostim` runs 35 checks, and all of them pass:
* both files match a fresh build and read back cleanly; ESL, only master Skyrim.esm, single light-range
  QUST, Start Game Enabled, alias forced to the player, script attached, alias entry points at the quest;
* form 0x801 in Stress and Fear.esp is GLOB `Stress_Total`;
* in OStim's sources: `FindActionSuperloadCSVv2` has exactly the 24 parameters after Id that the script passes,
  in order; `GetActionActor`, `GetActionTarget`, `OThread.GetActors`, `OThread.GetActor` exist with the
  assumed signatures; `ostim_orgasm` is documented as main-thread-only with (SceneID, Index, Sender);
* from OStim's action files, 15 sample actions land in the expected tier (handjob 5 … vaginalsex 20,
  nipple licking/sucking 5, kissing/cuddling 0);
* FMR's compiled `SendConceptionEvent(Actor akMother, Form akFather, Int iTrackingIndex)` creates
  `FMR_Conception` and pushes (mother, father, index).

## In-game test (pending)
1. Install the OStim option with OStim and Stress and Fear (and FMR) enabled. Load a save.
2. Console: `GetQuestRunning MoreStressGone_OStimQuest` → 1.
3. `set Stress_Total to 80` before each test, then check `GetGlobalValue Stress_Total` after the player's climax:
   * handjob scene with a stranger → 75
   * oral with a stranger → 70
   * anal with a stranger → 65
   * vaginal with a stranger → 60
   * vaginal with a Confidant/Ally (`setrelationshiprank player 3` on them) → 55
   * vaginal with your spouse → 50
4. A scene where you climax twice → it drops twice.
5. Partner climaxes but you don't → no change.
6. With FMR: after a conception involving you, the "deep sense of contentment" message appears and
   stress drops by 10 (FMR's MCM can force a conception to test this quickly).
7. `set Stress_Total to 3`, climax → 0 (never negative).
8. Save, quit, reload, repeat a test → still works.
9. Papyrus log: no errors mentioning `MoreStressGone_OStimScript`.

## Known limits
* The act is read from the finishing scene only. Acts earlier in the scene don't count, by design.
* In group scenes the partner bonus comes from the partner in the highest-value act.
* A climax forced with no sexual action in the scene still gives the 5.
