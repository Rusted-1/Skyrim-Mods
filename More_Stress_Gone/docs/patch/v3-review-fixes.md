# v1.1.1: review fixes

A review of 1.1.0 looked for anything the file-level checks didn't cover. It found one real problem,
fixed here, in how the hand-assembled scripts compare objects. No behaviour or numbers change.

## What was checked
| Check | Result |
|---|---|
| Quest VMAD layout (alias script, no quest script) against real Creation Kit plugins in my Data folder (Andrealletius Exploit Fixes, Biggie Traits, BiggieTraitsUI, Apocalypse) | Same bytes and same subrecord order as ours. OK |
| `ostim_orgasm` really sent by `OStim.dll` | The string is in the DLL. OK |
| Installed mods are the versions the patches were built and verified against (Stress and Fear.esp, ImGladYoureHere.esp, OStim.esp, Fertility Mode.esm, OStim.dll, and the scripts the patches rely on) | All byte-identical (md5). OK |
| How the real compiler compares objects | **Different from 1.1.0, so fixed** (below) |

## The fix: comparisons cast first
The real Papyrus compiler never compares two objects of different declared types directly. It first
casts one side to the other's type. That includes `None`, which it casts to the object's type:

```
callstatic game GetPlayer ::temp0 []
cast ::temp1 ::temp0                      ; Actor -> ObjectReference
cmp_eq ::temp2 akNewContainer ::temp1     ; (GYH WW42GYHCourierBallastScript)

cast ::temp0 None                         ; None -> Scene
cmp_eq ::temp1 currscene ::temp0          ; (GYH WW42HugFollowerScript)
```

1.1.0 compared `Form` directly with `Actor` in four places, and against a bare `None` in four more.
The game might handle that fine, or it might always treat those values as "not equal". The worst case:
* `akSender != player` in the OStim handler would always be true, so **the OStim patch would never lower stress**;
* the FMR mother/father checks would always be false, so **the conception bonus would never trigger**;
* the `None` checks are only safety checks, so failing them would be harmless while Stress and Fear is installed.

1.1.1 casts every comparison the way the compiler does:
* `OnOStimOrgasm`: `cast ::temp6 player` (to Form) before comparing with `akSender`; `cast ::temp0 None`
  before `partner == None`.
* `OnFMRConception`: player and player's ActorBase are cast to Form before comparing with
  `akMother` / `akFather`.
* `LowerStress` (OStim) and `OnGYHHug` (GYH): `None` is cast to GlobalVariable / Actor first.

The `.psc` sources don't change: they're what you'd write, and the compiler would add these casts itself.
The Glad You're Here script's bytes change, but it behaves the same.

## New guard in the tool
`Build.Check()` (run on every build) now also rejects any comparison whose operands have different
declared types or that uses a bare `None`. The new `check` command runs it on any `.pex`:
* the 1.1.0 scripts from git are **rejected** (`comparison of akSender (Form) with player (Actor) needs a cast`);
* the 1.1.1 scripts pass;
* **662 real compiled scripts** from Stress and Fear, Glad You're Here, OStim and FMR pass. The only 3 rejected
  are MCM scripts using `::Pages_var`, a variable inherited from SkyUI's `SKI_ConfigBase` that this check can't see.
  That confirms the rule matches the real compiler.

I also fixed the "undeclared identifier" check, which used to misread property names in `propget` /
`propset` and method names in `callparent` as variables.

## Verification
`verify-gyh` 17/17 and `verify-ostim` 35/35 pass on the rebuilt files.

## Still pending
In-game tests for both patches (`v1-design.md`, `v2-ostim.md`).
