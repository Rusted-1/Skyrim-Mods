# OInflation and Fertility Mode Reloaded for P.A.I.A

Backup of my Open Animation Replacer (OAR) patch for **P.A.I.A – Pregnancy and Inflation
Animation** and **P.A.I.A Expansion**. Out of the box, neither mod knows about **OInflation** or
**Fertility Mode Reloaded (FMR)**. P.A.I.A Expansion only checks the old Fertility Mode 3 Fixes,
Hentai Pregnancy, Fill Her Up and SGO4, and P.A.I.A's own Fertility Mode option needs Fertility
Mode 3 Fixes. With this patch the animations play when:

* OInflation has inflated a belly (its "Inflation Effect" is on the actor), or
* an FMR pregnancy has reached the same progress tiers P.A.I.A Expansion already uses.

The patch is condition files (`config.json`) only. Every submod points `overrideAnimationsFolder`
at a folder the original mod already installed, so it ships no animations and overwrites none of
P.A.I.A's or P.A.I.A Expansion's files.

**Needs the original mods.** This repo holds only my own work. It does not include P.A.I.A, P.A.I.A
Expansion, OInflation, Fertility Mode Reloaded or any of their files.

## Mods it needs
| Mod | Version checked | Used for |
|---|---|---|
| Open Animation Replacer | 2.3+ (the Expansion's random idles need it) | Reads the conditions |
| P.A.I.A – Pregnancy and Inflation Animation (Nexus 72085) | 4.5, **OAR** option | `PAIA Idle` animation |
| P.A.I.A Expansion OAR (Nexus 124228) | 1.2 | Walk/run/shield/idle animations |
| OInflation (Nexus 103761) | 1.0.1 | `OInflation.esp`, MGEF `002FA1` OInflationEffect |
| Fertility Mode Reloaded (Nexus 165569) | 1.0.7 | `Fertility Mode.esm`, FACT `02666B` ImmersiveEffectsFaction |

You only need the parts you use. Skip the OInflation option if you don't have OInflation, or pick
"None" on the Expansion page if you don't have P.A.I.A Expansion.

**Remove these if you have them**, because this patch replaces them:
* "P.A.I.A OInflation Patch for OAR" (Nexus 103761 optional file). It overwrites P.A.I.A's
  *OCum Inflated* condition so that it checks OInflation instead.
* "PAIA FMR" / "PAIA Expansion FMR" (Nexus 169023). They do the FMR half of this patch. Leaving
  them in won't break anything, but both sets of conditions would then compete.

## Install (Vortex)
Install `OInflation PAIA FMR Patch 1.0.0.zip`, or zip the contents of
`FOMOD/OInflation PAIA FMR Patch FOMOD/` yourself so that `fomod/` sits at the top of the zip.
Install it **after** P.A.I.A and P.A.I.A Expansion. Nothing in it conflicts with their files, so
Vortex shouldn't ask you for a rule. There's no plugin and no load order.

Installer pages:
1. **P.A.I.A.** Tick *OInflation* and/or *Fertility Mode Reloaded*. Each one is marked
   Recommended when its plugin is active.
2. **P.A.I.A Expansion.** Pick one:
   * **FMR + OInflation (recommended).** An OInflation belly plays the walk/run/shield sets and the
     "more than 10" and "more than 40" idles. OInflation's default maximum belly is 60%, so the
     heavy "more than 60/80" idles are left to FMR.
   * **FMR + OInflation (all tiers).** An OInflation belly counts for every tier. Use this if you
     raised OInflation's maximum belly size.
   * **None.**

Player-only: the two P.A.I.A submods include P.A.I.A's usual `PlayerKeyword` switch, which is off
by default. Turn it on in OAR's in-game editor (Shift+O). P.A.I.A Expansion already has separate
NPC and Player submods, and the patch keeps that split.

## How the conditions work
| Submod | Plays when |
|---|---|
| `PAIA\OInflation` (priority 91044067) | female, not a child, has OInflationEffect |
| `PAIA\Fertility Mode Reloaded` (priority 91044068) | female, not a child, FMR trimester 2/3 spell **or** ImmersiveEffectsFaction rank 33–100 |
| `PAIA Expansion\<set> - FMR OInflation` (upstream priority + 100000) | every original non-pregnancy condition of that set (sex, NPC/Player keyword, shield, combat, random chance), plus: not a child, and FMR rank ≥ the set's original tier and ≤ 100, **or** OInflationEffect (depending on the option) |

Details and the full tier table are in `docs/patch/v1-design.md`.

## What's here
| Folder | Contents |
|---|---|
| `FOMOD/OInflation PAIA FMR Patch FOMOD/` | The installer, unzipped: `fomod/` config, `PAIA/` (2 submods), `PAIA Expansion - Recommended/` and `PAIA Expansion - All Tiers/` (16 submods each) |
| `tools/` | `Build-Patch.ps1` (writes every `config.json`), `Verify-Patch.ps1` (file-level checks), `Get-PluginForms.ps1` (lists a plugin's EditorID/FormID pairs) |
| `docs/analysis/` | `01-how-the-mods-signal.md`: what P.A.I.A, the Expansion, OInflation and FMR expose |
| `docs/patch/` | `v1-design.md`: tiers, priorities, choices, in-game test plan |

## Rebuilding
Windows PowerShell 5.1, with no extra modules. Extract P.A.I.A Expansion OAR 1.2 somewhere, then from
inside `tools/`:

```
.\Build-Patch.ps1 -ExpansionDir "<extract>\Meshes\actors\character\animations\OpenAnimationReplacer\PAIA Expansion"
.\Verify-Patch.ps1 -PaiaDir "<P.A.I.A extract>\PAIA Fomod Installer\PAIA for SE\Meshes\Actors\Character\Animations\OpenAnimationReplacer\PAIA" `
  -ExpansionDir "<same as above>" -FmrPlugin "<path>\Fertility Mode.esm" -OInflationPlugin "<path>\OInflation.esp"
```

The script paths are all parameters, with nothing absolute baked in. The installer XML in `fomod/` is
hand-written, and `Build-Patch.ps1` doesn't touch it.

## Status
v1.0.0. Built and verified at file level: `Verify-Patch.ps1` passes all 348 checks. These cover the
form IDs against the real plugins, the animation folders against the installed mods, upstream
conditions carried over unchanged, the tiers, the OInflation inclusion per option, unique
priorities, the installer paths, and that only .json/.xml files ship. **Not yet tested in game.**
The test steps are at the end of `docs/patch/v1-design.md`.
