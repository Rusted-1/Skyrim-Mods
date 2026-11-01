# Stress and Fear 2.07: where stress lives

Source: `Stress and Fear.esp` (ESL-flagged) and `Source/Scripts/*.psc` from the 2.07 file
(Nexus 116522).

## The stress value
Stress is one global, **`Stress_Total`** (GLOB `xx000801`), from 0 to 100. There's no API
function. Every script in the mod reads and writes the global directly:

* `StressAndPhobiasScript` (on the player alias of `Stress_Quest`) raises it on hits in combat
  and lowers it when you drink potions with the `Stress_Reduce10/25/50` keywords. After lowering it,
  it clamps the value to 0 and shows a "stress decreased" message if you were above 25 beforehand.
* `Stress_SpellTracker` (magic effect) adds `PercentageToReduce` (negative = reduce), clamps to
  0, then calls the alias's `StressHasDecreased()` / `ReportCurrentStress()`.
* The MCM shows `Stress_Total` as "Current Stress n/100" and can reset it to 0.

Other globals: `Stress_Rate` (xx0008A7), `Stress_Enabled` (xx0008A5, gates the messages),
`Stress_FearEnabled`, `Stress_BoxMessages`.

## What that means for a patch
Lowering `Stress_Total` and clamping it at 0 is exactly what the mod itself does, so it's
the safest way to lower stress from outside. `Game.GetFormFromFile(0x801, "Stress and Fear.esp")`
finds the global without making Stress and Fear a master.

## Template for the patch plugin
`Stress_Quest` (xx000800) is a start-game-enabled, run-once quest (DNAM flags `0x11`, priority
50) with one alias forced to `PlayerRef`, scripted through VMAD. Its VMAD showed the layout of
the alias-script block (fragment version 2, 0 fragments, empty file name, alias count,
alias object, then the alias's scripts). The patch's quest copies that layout.
