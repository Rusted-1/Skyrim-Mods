# More Stress Gone

Patches for **Stress and Fear** (Skyrim Special Edition) that let more things from my other mods
lower your stress. Each supported mod is a separate option in one FOMOD installer, with its own
light plugin and script, so you install only the ones you use.

**Needs the original mods.** This repo holds only my own work. It does not include Stress and
Fear, Glad You're Here, OStim, Fertility Mode Reloaded or any other author's plugins, scripts or
assets.

## Patches
### Glad You're Here (since 1.0.0)
Hugging someone with Glad You're Here lowers your stress:

| Who you hug | Stress lost |
|---|---|
| Your children | 25 |
| Your spouse or lover | 18 |
| Good friends (relationship rank Confidant or Ally) | 10 |
| Followers, strangers, pets | 5 |

Plugin `MoreStressGone_GladYoureHere.esp`. Its script listens for the `GYH_OnHugActor` event that
Glad You're Here sends after every hug. Message: "The hug eases your stress."

### OStim (since 1.1.0)
Every time your character climaxes in an OStim scene, stress goes down. The amount is based on the
scene playing at that moment (the finishing scene). Of the acts your character takes part in there,
the highest-value one counts:

| Act (by OStim action tags) | Stress lost |
|---|---|
| Vaginal intercourse | 20 |
| Anal intercourse | 15 |
| Oral (not counting nipple licking/sucking) | 10 |
| Manual and every other sexual act | 5 |

Then a bonus based on your partner in that act:

| Partner | Bonus |
|---|---|
| Spouse or lover (relationship rank 4) | +10 |
| Good friend (Confidant or Ally) | +5 |
| Anyone else | +0 |

With **Fertility Mode Reloaded**, a conception where your character is the mother or the father
lowers stress by another **10**. The message for it, "You feel a deep sense of contentment.",
doesn't mention pregnancy, so FMR's own reveal isn't spoiled.

Every climax counts. If a child is anywhere in the scene, nothing happens. Message: "You feel the
tension drain away." The full list of OStim actions and what each counts as is in
`docs/patch/v2-ostim.md`.

Plugin `MoreStressGone_OStim.esp`. Its script listens for OStim's `ostim_orgasm` event and FMR's
`FMR_Conception` event.

### Both
Stress never goes below 0. Neither original mod is edited. Each plugin is ESL-flagged and has a
single quest whose player alias runs the script.

## Mods it needs
| Mod | Plugin | Needed for |
|---|---|---|
| SKSE64 | | both |
| Stress and Fear 2.07 (Nexus 116522) | `Stress and Fear.esp` | both |
| Glad You're Here 3.6.0 (Nexus 41856) | `ImGladYoureHere.esp` | Glad You're Here patch |
| OStim Standalone 7.5c (Nexus 98163) | `OStim.esp` | OStim patch |
| Fertility Mode Reloaded 1.0.7 (Nexus 165569) | `Fertility Mode.esm` | OStim patch, optional (conception bonus) |

## Load order
Doesn't matter. Each plugin's only master is Skyrim.esm. The scripts find the other mods' forms and
events while the game runs, so the order of the plugins doesn't matter. If a mod is missing, its
part simply does nothing.

## Install
Zip the contents of `FOMOD/More Stress Gone FOMOD/` (so `fomod/`, `GladYoureHere/` and `OStim/`
sit at the top of the zip) and install the zip through Vortex. Tick the patches you want. Deploy,
then check in the Plugins tab that the plugins are **enabled**.

It's safe to add to an existing save: each quest starts by itself the next time the save loads.

## Check it's working
* Console: `GetQuestRunning MoreStressGone_GYHQuest` / `GetQuestRunning MoreStressGone_OStimQuest`
  should print `1`.
* `GetGlobalValue Stress_Total` before and after should show the drop.

## What's here
| Folder | Contents |
|---|---|
| `FOMOD/More Stress Gone FOMOD/` | The installer, unzipped: `fomod/` config, `GladYoureHere/` and `OStim/` (plugin, compiled script, script source each) |
| `tools/MsgTool/` | C# (.NET 8) tool: builds the plugins and assembles the compiled scripts, checks them against the original mods, reads/disassembles `.pex` and `.esp` files, zips the installer |
| `docs/analysis/` | How each original mod works where the patches hook in |
| `docs/patch/` | Design, checks and in-game test plan per version: `v1-design.md` (Glad You're Here), `v2-ostim.md` (OStim), `v3-review-fixes.md` (1.1.1 fixes) |

## Rebuilding
Needs the .NET 8 SDK. There's no Creation Kit or Papyrus compiler on my PC, so the tool writes
the plugins and the compiled `.pex` files itself. Each `.psc` in `Source/Scripts/` is the readable
version of its script. `tools/MsgTool/GYHPatch.cs` and `OStimPatch.cs` assemble the `.pex` from it
line by line, so if you change one, change the other to match. From the `More_Stress_Gone` folder:

```
dotnet run --project tools/MsgTool -c Release -- build "FOMOD/More Stress Gone FOMOD"
dotnet run --project tools/MsgTool -c Release -- verify-gyh "FOMOD/More Stress Gone FOMOD" "<Stress and Fear.esp>" "<ImGladYoureHere.esp>" "<GYH Scripts/WW42HugFollowerScript.pex>"
dotnet run --project tools/MsgTool -c Release -- verify-ostim "FOMOD/More Stress Gone FOMOD" "<Stress and Fear.esp>" "<unzipped OStim folder>" "<FMR scripts folder>"
dotnet run --project tools/MsgTool -c Release -- zip "FOMOD/More Stress Gone FOMOD" "More Stress Gone 1.1.1.zip"
```

`ostim-table "<unzipped OStim folder>"` prints which OStim actions count for how much. `check "<file.pex or folder>"` runs the
assembler's sanity checks (declared names, jump targets, matching types in comparisons) on any compiled script.

## Status
1.1.1. Built and verified at the file level:
* Glad You're Here: all 17 checks in `verify-gyh` pass.
* OStim: all 35 checks in `verify-ostim` pass against OStim Standalone 7.5c and Fertility Mode
  Reloaded 1.0.7.

The tool also reads and re-writes every compiled script in Stress and Fear and Glad You're Here
byte-for-byte, and it disassembles every FMR script without error. **Neither patch has been tested
in game yet.** The test steps are in `docs/patch/`.
