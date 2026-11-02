# OStim Standalone 7.5c: climax events and act data

Source: the 7.5c archive (Nexus 98163): `Scripts/Source/*.psc` and the data files in
`SKSE/Plugins/OStim/` (`list of mod events.txt`, `actions/*.json`, `actions README.txt`).

## The hook: `ostim_orgasm`
From `list of mod events.txt`, under "Main Thread Events" ("these events only fire on the main
thread (the thread involving the player)"):

```papyrus
Event OStimOrgasm(string EventName, string SceneID, float Index, Form Sender)
	; SceneID: the scene the thread is in right now
	; Index:   the position in that scene of the actor that climaxed
	; Sender:  the actor that climaxed
EndEvent
```

It fires once per climax. OStim's own `OSexIntegrationMain` uses the same event the same way:
`OMetadata.FindActionForActor(sceneId, index as int, "vaginalsex")` and then
`GetActor(OMetadata.GetActionTarget(...))` decide who gets Fertility Mode's `FertilityModeAddSperm`
event. The main thread's ID is 0 (`OThread.GetActors(0)` in the same script).

There's also `ostim_actor_orgasm` (all threads, passes ThreadID instead of the actor's index). The patch
doesn't need it, because only the player's thread matters.

## Acts are described by tagged actions
Each scene lists actions (actor, target, performer positions plus an action type). Each action type
in `actions/*.json` has tags. The ones the patch uses:

| Tag | On |
|---|---|
| `intercourse` + `vaginalpenetration` | vaginalsex, vaginaltailsex |
| `intercourse` + `analpenetration` | analsex, analtailsex |
| `oral` | blowjob, deepthroating, penilelicking, vulvaleating, vulvallicking, rimjob, anallicking, testicularlicking, and also lickingnipple / suckingnipple (those also have `nipplestimulation`) |
| `sexual` | every sexual action (handjob, footjob, fingering, toying, masturbation, …), plus all of the above |
| (no `sexual`) | kissing, cuddling, holding, massaging and other non-sexual actions |

Toys, fingers and fisting carry `vaginalpenetration`/`analpenetration` but not `intercourse`.

`OMetadata.FindActionSuperloadCSVv2` searches a scene for an action by participant position (actor,
target or performer) and any/all/blacklisted tags. It returns the action's index, or -1. Because
it works on tags rather than a fixed list of action names, it also covers acts that OStim add-ons
define with the same tags. `GetActionActor` / `GetActionTarget` give the positions of the two sides,
and `OThread.GetActor(0, position)` gives the actor.
