# More Stress Gone

Patches for **Stress and Fear** (Skyrim Special Edition) that let more things from my other mods
lower your stress. Each supported mod is a separate option in one FOMOD installer.

**Needs the original mods.** This repo holds only my own work. It does not include Stress and
Fear, Glad You're Here or any other author's plugins, scripts or assets.

## Patches
### Glad You're Here (1.0.0)
Hugging someone with Glad You're Here lowers your stress:

| Who you hug | Stress lost |
|---|---|
| Your children | 25 |
| Your spouse or lover | 18 |
| Good friends (relationship rank Confidant or Ally) | 10 |
| Followers, strangers, pets | 5 |

A short "The hug eases your stress." notification shows when your stress drops. Stress never
goes below 0.

It's one light (ESL-flagged) plugin, `MoreStressGone_GladYoureHere.esp`, with one quest. That
quest's player alias runs `MoreStressGone_GYHScript`, which listens for the `GYH_OnHugActor` event
that Glad You're Here sends after every hug. Neither original mod is edited.

## Mods it needs
| Mod | Plugin |
|---|---|
| SKSE64 | (for mod events) |
| Stress and Fear 2.07 (Nexus 116522) | `Stress and Fear.esp` |
| Glad You're Here 3.6.0 (Nexus 41856) | `ImGladYoureHere.esp` |

## Load order
Doesn't matter. The plugin's only master is Skyrim.esm. It finds the two mods' forms while the
game runs (`Game.GetFormFromFile`), so the order of the plugins doesn't matter. If either mod is
missing, hugs simply do nothing extra.

## Install
Zip the contents of `FOMOD/More Stress Gone FOMOD/` (so `fomod/` and `GladYoureHere/` sit at the
top of the zip) and install the zip through Vortex. Tick **Glad You're Here** in the installer.
Deploy, then check in the Plugins tab that `MoreStressGone_GladYoureHere.esp` is **enabled**.

It's safe to add to an existing save: the quest starts by itself the next time the save loads.

## Check it's working
* In the console, `GetQuestRunning MoreStressGone_GYHQuest` should print `1`.
* `GetGlobalValue Stress_Total` before and after a hug should show the drop.

## What's here
| Folder | Contents |
|---|---|
| `FOMOD/More Stress Gone FOMOD/` | The installer, unzipped: `fomod/` config and `GladYoureHere/` (plugin, compiled script, script source) |
| `tools/MsgTool/` | C# (.NET 8) tool: builds the plugin and assembles the compiled script, checks them against the original mods, reads/disassembles `.pex` and `.esp` files, zips the installer |
| `docs/analysis/` | How Glad You're Here hugs work and how Stress and Fear stores stress |
| `docs/patch/` | `v1-design.md`: the records, the script, the checks, the in-game test plan |

## Rebuilding
Needs the .NET 8 SDK. There's no Creation Kit or Papyrus compiler on my PC, so the tool writes
the plugin and the compiled `.pex` itself. The `.psc` in `Source/Scripts/` is the readable
version of the script. `tools/MsgTool/Build.cs` (`GYHScript()`) assembles the `.pex` from it line by
line, so if you change one, change the other to match. From the `More_Stress_Gone` folder:

```
dotnet run --project tools/MsgTool -c Release -- build "FOMOD/More Stress Gone FOMOD/GladYoureHere"
dotnet run --project tools/MsgTool -c Release -- verify "FOMOD/More Stress Gone FOMOD/GladYoureHere" "<path to Stress and Fear.esp>" "<path to ImGladYoureHere.esp>" "<path to WW42HugFollowerScript.pex>"
dotnet run --project tools/MsgTool -c Release -- zip "FOMOD/More Stress Gone FOMOD" "More Stress Gone 1.0.0.zip"
```

`WW42HugFollowerScript.pex` is in Glad You're Here's `Scripts/` folder.

## Status
1.0.0, Glad You're Here patch only. Built and verified at the file level: all 17 checks in
`verify` pass against Stress and Fear 2.07 and Glad You're Here 3.6.0. The tool also reads and
re-writes every compiled script in both mods byte-for-byte, which tests its `.pex` format
handling. **Not yet tested in game**. The test steps are at the end of `docs/patch/v1-design.md`.
