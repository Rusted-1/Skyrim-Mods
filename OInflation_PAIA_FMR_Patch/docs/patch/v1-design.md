# v1.0.0 design

## Goal
Make P.A.I.A and P.A.I.A Expansion play their animations for OInflation bellies and Fertility Mode
Reloaded pregnancies. Use the same pregnancy tiers the Expansion already uses, and don't copy or
overwrite any of the original files.

## Approach
OAR loads every subfolder of a mod folder that has a `config.json` as a submod. A submod can borrow a
sibling folder's animations with `overrideAnimationsFolder`, so each patch submod is one
`config.json` installed **next to** the original submods.

* No `.hkx` is shipped, and no original `config.json` is replaced.
* The original submods stay as they are. For an FMR/OInflation user their pregnancy checks just
  never pass, because those plugins aren't loaded. OAR treats a form from a missing plugin as false.
* Uninstalling the patch leaves the originals exactly as they were.

## P.A.I.A submods
| Folder | Priority | Conditions |
|---|---|---|
| `PAIA\OInflation` | 91044067 | [PlayerKeyword, disabled] · IsFemale · NOT IsChild · HasMagicEffect `OInflation.esp` 2FA1 |
| `PAIA\Fertility Mode Reloaded` | 91044068 | [PlayerKeyword, disabled] · IsFemale · NOT IsChild · OR( HasSpell 1B818, HasSpell 1B81A, AND(rank 02666B ≥ 33, ≤ 100) ) |

Rank 33 matches P.A.I.A's own "2nd or 3rd trimester" Fertility Mode check. 91044067 is the number
the earlier OInflation patch used, and 91044068 is the next free one.

## P.A.I.A Expansion submods
`Build-Patch.ps1` reads each of the 16 upstream configs and writes `<name> - FMR OInflation`:

* All non-pregnancy conditions are copied unchanged and in order, with `NOT IsChild` added after
  `IsFemale`. P.A.I.A has that check, but the Expansion doesn't.
* The OR block becomes:
  `OR( [FMR trimester spells, only where upstream had them], AND(rank 02666B ≥ tier, ≤ 100), [OInflationEffect] )`
* `overrideAnimationsFolder` = the upstream folder.
* `priority` = upstream + 100000. This keeps the upstream order between sets (shield > non-combat
  > idles > base) and puts every patch submod above every original one.

| Set | Tier (FMR rank ≥) | OInflation, recommended | OInflation, all tiers |
|---|---|---|---|
| female fhu base | 50 | yes | yes |
| female fhu base player | 40 | yes | yes |
| female fhu noncombat / Player | 40 | yes | yes |
| female fhu 1h shield / player | 40 | yes | yes |
| female fhu 1h shield noncombat / player | 40 | yes | yes |
| female fhu Idle more than 10 / Player | 10 | yes | yes |
| female fhu Idle more than 40 / Player | 40 | yes | yes |
| female fhu Idle more than 60 / Player | 60 | no | yes |
| female fhu Idle more than 80 / Player | 80 | no | yes |

OInflation can't report a size to OAR, only whether the effect is on (see
`docs/analysis/01-how-the-mods-signal.md`). Its default maximum belly is 60%, so the
recommended option treats an OInflation belly as tier ≤ 50. The all-tiers option is there for
anyone who raised the maximum.

The `≤ 100` cap copies the PAIA FMR example patches, so a rank above 100 never counts as pregnant.

## Verification (file level)
`tools/Verify-Patch.ps1`: 348 checks, all passing.
* The four referenced forms exist with the expected type and EditorID in `Fertility Mode.esm`
  1.0.7 and `OInflation.esp` 1.0.1. No other form is referenced.
* P.A.I.A: the animation folder exists, priorities don't collide with P.A.I.A's, and the
  player-only switch is off.
* Expansion (both options): one submod per upstream submod, the override folder matches upstream
  and has animations, priority is upstream + 100000, the non-pregnancy conditions match upstream
  exactly, NOT IsChild is present, the FMR tier matches the upstream tier with a ≤ 100 cap,
  OInflation is included exactly per the table, and priorities are unique.
* The installer's XML parses, every source folder exists, every destination is under
  `OpenAnimationReplacer\PAIA*`, and only .json/.xml files are shipped.

## In-game test (pending)
1. Install P.A.I.A (OAR), P.A.I.A Expansion, OInflation, FMR, then this patch (recommended).
   In OAR's editor (Shift+O), the "PAIA" mod should list *OInflation* and *Fertility Mode
   Reloaded*, and "PAIA Expansion" should list 16 extra "- FMR OInflation" submods with no
   errors.
2. **OInflation:** finish an OStim scene that inflates the player. While the belly is visible,
   the walk/run set should switch to the P.A.I.A Expansion one, and the OAR editor should show
   `female fhu base player - FMR OInflation` as active. After the cleanup timer the belly goes
   and the normal animations return.
3. **FMR:** get the player pregnant and advance time (or use FMR's debug impregnate). Check that
   `GetFactionRank` on ImmersiveEffectsFaction rises (console:
   `player.getfactionrank xx02666B`, where `xx` is FMR's load-order index). The base set should start
   at rank 40, and the 60/80 idles once the rank passes those tiers.
4. Repeat 2–3 on a female NPC. The NPC sets (base at 50) should trigger.
5. If shield, combat and random-idle sets don't change when expected, open the submod in OAR's
   editor to see which condition fails.

## Known limits
* OInflation has no size signal, so the Expansion tiers can't follow the OInflation belly size.
* With OCum Ascended, OInflation only inflates actors in a scene with the player. NPC-only scenes
  use a different event that OInflation doesn't listen to (see the analysis doc). The patch
  can't change that.

## Review (2026-11-02)
* The FMR rank meaning is now confirmed from FMR's compiled scripts: 1–100 = pregnancy progress,
  0 = not pregnant, and 101–120 = after birth / cycle / labor (full table in
  `docs/analysis/01-how-the-mods-signal.md`). The `≤ 100` cap is required, not just a safeguard.
* Open Animation Replacer 3.2.0 has every condition the patch uses (AND, OR, FactionRank,
  HasMagicEffect, HasSpell, IsEquippedType, Random, IsInCombat).
* The release zip matches the verified files byte for byte (36 files). There's no BOM, and all
  JSON parses. The longest installed path is 138 characters, about 200 with the game folder.
