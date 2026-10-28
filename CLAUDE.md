# Instructions for Claude: backing up Skyrim mods here

This repo (`Rusted-1/Skyrim-Mods`) is the single backup for all of my own Skyrim mods and
patches. Each mod lives in its own folder. The top-level `README.md` is the index.

## Golden rules
1. **Only my own work.** Never commit other authors' files: their plugins (.esp/.esm/.esl), BSA
   archives, meshes, textures, music, sounds, or their installers. Also never commit a re-encoded or edited
   copy of their assets. Some authors forbid
   redistribution outright.
2. **One folder per mod.** Never mix two mods in one folder.
3. **Keep the index current.** Every time a mod folder is added or its status changes, update the
   table in the top-level `README.md`.
4. **Push straight to `main`.** It's a solo backup repo, so no branches or pull requests are needed.

## Folder layout for a mod
Name the folder `Words_With_Underscores` after the mod, for example `Music_Mods_Combined`
(the Fallout_4_Mods repo has more examples).

| Path | What goes in it |
|---|---|
| `README.md` | Required. What the mod does, which mods it needs, load order, how to install, how to rebuild, status / what's untested |
| `FOMOD/<package folder>/` | The installer **unzipped** (`fomod/` config plus plugin folders). Unzipped so diffs show what changed |
| `tools/` | Any scripts used to read or build plugins, plus small v1 input plugins they need (only if they're my own) |
| `docs/analysis/` | Findings about the original mod(s): stats, bugs, how things worked before the patch. Number the files: `01-…md`, `02-…md` |
| `docs/patch/` | Design and changelog per version: `v1-design.md`, `v2-…md` |
| `notes/` | Optional. My rough working notes |

## What to leave out
- `__pycache__/`, `*.pyc`, cache or pickle files (`*.pkl`), extracted copies of the source mods
- Zipped installers (the unzipped `FOMOD/` folder is the backup; zip it to rebuild)
- Anything over ~50 MB
- If a script has absolute paths from your session, either fix them or note it in the README

## How to back up a mod
1. Add the repo to your session (`Rusted-1/Skyrim-Mods`, push access) and clone it.
2. Read the top-level `README.md` and one existing mod folder's `README.md` to match the style.
3. Create the mod's folder with the layout above, then copy in the installer, scripts and docs.
4. Write the mod's `README.md`, then add its row to the top-level index table.
5. Check `git status`: nothing from the "leave out" list, and no other authors' files.
6. Commit to `main` with a clear message, e.g. `Add <Mod Name> v1.0 backup` or
   `Update <Mod Name> to v1.1`, and push.
7. Tell me what was added and anything that was left out on purpose.

Updating an existing mod works the same way: edit its folder, add a new `docs/patch/vN-…md` for
the version, and update its status in both READMEs.
