# Glad You're Here 3.6.0: how hugs work

Source: `ImGladYoureHere.esp` and `Source/Scripts/WW42HugFollowerScript.psc` from the 3.6.0 main
file (Nexus 41856).

## Two hug quests, one script
Every hug goes through `WW42HugFollowerScript.HugActor(Actor akTarget)`. Two quests carry that
script, told apart by its `NPC` property (values read from the plugin's VMAD):

| Quest | FormID | `NPC` | Who it hugs |
|---|---|---|---|
| `WW42HugFollower` | `xx000D62` | 0 | followers, friends, strangers, pets (the script comment says "follower script (includes animals)") |
| `WW42HugFamily` | `xx21AF77` | 1 | spouse and adopted children ("spouse script (includes children)") |

Inside `HugActor` the target is classed as a child (`IsChild()`, or `actorType == 1` set by
`HugChildActor`), an animal (creature/dog/potential-animal faction) or an adult. The family quest
also gives the Lover's / Parent's Embrace boons, and the follower quest gives Friendship's Embrace.

## The hook: `GYH_OnHugActor`
At the very end of `HugActor`, after the animation, boons and letters bookkeeping:

```papyrus
int handle = ModEvent.Create("GYH_OnHugActor")
if (handle)
    ModEvent.PushForm(handle, self)      ; the hug quest (WW42HugFollower or WW42HugFamily)
    ModEvent.PushForm(handle, akTarget)  ; the actor hugged
    ModEvent.Send(handle)
endIf
```

The compiled `.pex` matches (`verify` checks it). So another mod can register for
`GYH_OnHugActor` with a callback `Event X(Form sender, Form target)`. The sender says whether
it was a family hug, without touching any GYH file.

## Notes
* The event fires even if the animation was skipped (for example, the target is in a scene and
  scene protection blocks it). That's rare, and the patch treats it as a hug.
* Horse hugs (`HugHorse`, via a perk) don't send the event, so they don't lower stress.
