Scriptname MoreStressGone_GYHScript extends ReferenceAlias
{More Stress Gone - Glad You're Here patch.
Lowers Stress and Fear's stress every time you hug someone with Glad You're Here.
Attached to the player alias of MoreStressGone_GYHQuest.}

; NOTE: there is no Papyrus compiler on the build machine. MoreStressGone_GYHScript.pex is
; assembled instruction by instruction from this file by tools/MsgTool (Build.cs, GYHScript()).
; Keep the two in step if you change either.

Event OnInit()
	RegisterForModEvent("GYH_OnHugActor", "OnGYHHug")
EndEvent

Event OnPlayerLoadGame()
	RegisterForModEvent("GYH_OnHugActor", "OnGYHHug")
EndEvent

; Glad You're Here sends GYH_OnHugActor at the end of every hug, with (hug quest, actor hugged).
; Its WW42HugFamily quest handles your spouse and children; WW42HugFollower handles everyone else.
Event OnGYHHug(Form akSender, Form akTarget)
	Actor target = akTarget as Actor
	GlobalVariable stressTotal = Game.GetFormFromFile(0x00000801, "Stress and Fear.esp") as GlobalVariable
	If target == None || stressTotal == None
		Return
	EndIf

	Int amount = 5                                     ; followers, strangers, animals
	Form familyQuest = Game.GetFormFromFile(0x0021AF77, "ImGladYoureHere.esp") ; WW42HugFamily
	If akSender == familyQuest && target.IsChild()
		amount = 25                                    ; your children
	ElseIf akSender == familyQuest || target.GetRelationshipRank(Game.GetPlayer()) >= 4
		amount = 18                                    ; spouse / lover
	ElseIf target.GetRelationshipRank(Game.GetPlayer()) >= 2
		amount = 10                                    ; good friend (confidant or ally)
	EndIf

	Float before = stressTotal.GetValue()
	Float after = before - amount
	If after < 0.0
		after = 0.0
	EndIf
	stressTotal.SetValue(after)

	If before > after
		Debug.Notification("The hug eases your stress.")
	EndIf
EndEvent
