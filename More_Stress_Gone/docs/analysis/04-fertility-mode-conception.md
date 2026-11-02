# Fertility Mode Reloaded 1.0.7: the conception event

Source: the 1.0.7 archive (Nexus 165569). It ships no `.psc` sources, so this comes from
disassembling its compiled scripts with `MsgTool dis`.

## `FMR_Conception`
`_JSW_BB_Utility.SendConceptionEvent(Actor akMother, Form akFather, Int iTrackingIndex)`:

```
ModEvent.Create("FMR_Conception")
PushForm(akMother)
PushForm(akFather)
PushInt(iTrackingIndex)
Send()
```

It's called whenever a pregnancy starts:
* `_JSW_BB_ImpregnateEffect` (normal conception),
* `_JSW_BB_HandlerQuestAliasScript` (the `FertilityModeConception` mod event other mods use to force one),
* `_JSW_BB_ConfigQuestScript` (MCM/debug pregnancy for the player or an NPC).

So a listener sees `(Form mother, Form father, Int index)`. The father comes from FMR's
`CurrentFatherRef` storage, which normally holds the father's actor. It can be None when the father isn't
known, for example after a forced conception.

## Timing
Conception isn't instant. FMR tracks sperm per actor (OStim sends it `FertilityModeAddSperm` on
vaginal climaxes; FMR also listens to `ostim_actor_orgasm`) and rolls conception later, in game
time. So `FMR_Conception` usually arrives hours after the scene, which is why the patch gives this
bonus separately from the climax reward.

FMR also has other events (`FMR_BabyBirth`, `FMR_Abort`, `FMR_PregnancyCancelled`, `FMR_ActorStatus`,
…) that later patches could use.
