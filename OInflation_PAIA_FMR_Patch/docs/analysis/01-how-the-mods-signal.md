# 01 – What each mod exposes to OAR

Sources: P.A.I.A 4.5 (Nexus 72085), P.A.I.A Expansion OAR 1.2 (Nexus 124228), OInflation 1.0.1
(Nexus 103761), Fertility Mode Reloaded 1.0.7 (Nexus 165569). The plugins were read with
`tools/Get-PluginForms.ps1`. OInflation ships its Papyrus sources. FMR ships only compiled
scripts, so its scripts were read for their string tables only.

## P.A.I.A 4.5 (OAR option)
Folder `OpenAnimationReplacer\PAIA\`. One animation folder, `PAIA Idle\female\mt_idle.hkx`, and
condition-only submods that point at it with `overrideAnimationsFolder: "PAIA Idle"`:

| Submod | Priority | Checks |
|---|---|---|
| Fertility Mode | 91044060 | FM trimester 2/3 spells (`Fertility Mode.esm` 1B818/1B81A) or `Fertility Mode 3 Fixes and Updates.esp` 862 rank ≥ 33 |
| Fill Her Up | 91044061 | `sr_FillHerUp.esp` spells/effect |
| OCum Inflated | 91044065 | `OCum.esp` MGEF F18 |

The installer only offers "Fertility Mode" when `Fertility Mode 3 Fixes and Updates.esp` is
active, so FMR users can't use it. The file `Priority List OAR.txt` reserves 91044060–91044066.
The earlier OInflation patch used 91044067.

## P.A.I.A Expansion OAR 1.2
Folder `OpenAnimationReplacer\PAIA Expansion\`. There are 16 submods, each with its own animations
and an `overrideAnimationsFolder` that points at itself. Each one has a fixed set of
non-pregnancy conditions (IsFemale, `ActorNPC` or `PlayerKeyword`, sometimes left-hand shield,
not-in-combat, or a 35.71% random roll) and **one OR block** of pregnancy checks:

| Mod in the OR block | Form | Used as |
|---|---|---|
| Fertility Mode 3 Fixes and Updates | FACT 862 | rank ≥ tier |
| Hentai Pregnancy | FACT 12085 | rank ≥ 1/2/3 |
| Fill Her Up | FACT A991 | rank ≥ tier |
| SGO4 Integration Fork | FACT 8E425 | rank ≥ tier |
| (Idle > 60 Player only) Fertility Mode | SPEL 1B818 / 1B81A | has spell |

Tiers (the Fertility Mode 3 threshold): base NPC 50, base Player 40, shield/non-combat 40, idles
10 / 40 / 60 / 80. Neither OInflation nor FMR's faction appears anywhere.

Animation folders: base, non-combat and shield sets hold `1hm_*`, `2hm_*` and `mt_*` walk/run
files. Every "Idle more than N" folder (NPC and Player) holds `female\mt_idle.hkx`, with
`mt_walk/runforward` added at 60/80. The Player folders also have `sneakmtidle.hkx`.

## OInflation 1.0.1
* `OInflation.esp`: MGEF `002FA1` **OInflationEffect**, SPEL `002FA3` OInflationSpell, KYWD
  `002FA2` OInflationAppliedInflation.
* `OInflationScript.psc`: during an OStim scene it grows the `PregnancyBelly` morph. When the actor
  leaves the scene (`OStimActorLeave` / `OStimEnd`) it casts OInflationSpell on them. The spell's
  duration is `InflationCleanupTimer` minutes, and when it ends the morph is removed. So
  **"has OInflationEffect" = "belly is inflated after a scene"**.
* There's no rank or global for the belly size. It only lives in the NiOverride morph, which OAR
  can't read. Default max size (MCM) is 60%.
* OInflation listens for OCum's `ocum_applied_cum` event (Form, Form, Float, String, String).
  **OCum Ascended** 4.1.1 still sends it with those arguments (`OCumMaleScript.SendCumAppliedEvents`),
  but only for the player's scene (thread 0). NPC-only scenes send `ocum_applied_cum_npc_scene`
  instead, which OInflation doesn't listen to. So with OCum Ascended, only actors in a scene with
  the player get inflated.

## Fertility Mode Reloaded 1.0.7
* Keeps the original plugin name, `Fertility Mode.esm`.
* FACT `02666B` **ImmersiveEffectsFaction**. Every `SetFactionRank` call on it, read from the
  compiled scripts with `tools/Show-PexCalls.ps1`:

  | Rank | Set by | Meaning |
  |---|---|---|
  | 0 | `_JSW_BB_Utility.ClearActorEffects`, end of recovery, miscarriage | not pregnant |
  | 1 | `_JSW_BB_ImpregnateEffect.OnEffectStart` | just conceived |
  | **1–100** | `UpdatePregnancy`: `1 + int(pregnantDay / duration × 99)`, capped at 100 | **pregnancy progress** |
  | 101 | `GiveBirth` | just gave birth |
  | 101–115 | `UpdateStatusSingle`: `101 + int(recovery progress × 14)` | recovering after birth |
  | 117–119 | `UpdateStatusSingle` | menstrual cycle phase |
  | 120 | `OnFertilityModeLabor` | in labor |

  So "pregnant" is exactly rank 1–100. Any check also needs `≤ 100`, otherwise recovery, cycle
  and labor ranks pass a `≥ tier` test.
* SPEL `01B816` / `01B818` / `01B81A`: trimester 1/2/3 spells (same IDs as the original Fertility
  Mode, still referenced by `_JSW_BB_Utility`).
