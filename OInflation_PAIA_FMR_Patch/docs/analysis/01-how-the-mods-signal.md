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

## Fertility Mode Reloaded 1.0.7
* Keeps the original plugin name, `Fertility Mode.esm`.
* FACT `02666B` **ImmersiveEffectsFaction**. `_JSW_BB_HandlerQuestAliasScript`, `_JSW_BB_Utility`
  and `_JSW_BB_ImpregnateEffect` all call `SetFactionRank` on it, together with strings like
  `PREGNANCY SET`, `rank=` and `FMR_LastSentRank`. The rank is the pregnancy progress, 0–100.
  Both "PAIA FMR" example patches (Nexus 169023) use it the same way, with a rank of 1–100
  meaning pregnant.
* SPEL `01B816` / `01B818` / `01B81A`: trimester 1/2/3 spells (same IDs as the original Fertility
  Mode, still referenced by `_JSW_BB_Utility`).
