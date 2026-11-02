Scriptname MoreStressGone_OStimScript extends ReferenceAlias
{More Stress Gone - OStim patch.
Lowers Stress and Fear's stress each time the player climaxes in an OStim scene, and again when
Fertility Mode Reloaded reports a conception involving the player.
Attached to the player alias of MoreStressGone_OStimQuest.}

; NOTE: there is no Papyrus compiler on the build machine. MoreStressGone_OStimScript.pex is
; assembled instruction by instruction from this file by tools/MsgTool (Build.cs, OStimScript()).
; Keep the two in step if you change either.

Event OnInit()
	RegisterEvents()
EndEvent

Event OnPlayerLoadGame()
	RegisterEvents()
EndEvent

Function RegisterEvents()
	RegisterForModEvent("ostim_orgasm", "OnOStimOrgasm")
	RegisterForModEvent("FMR_Conception", "OnFMRConception")
EndFunction

; OStim sends ostim_orgasm on the player's thread (thread 0) whenever an actor in it climaxes.
; asSceneID is the scene playing at that moment, afIndex the climaxing actor's position in it.
Event OnOStimOrgasm(String asEventName, String asSceneID, Float afIndex, Form akSender)
	Actor player = Game.GetPlayer()
	If akSender != player
		Return
	EndIf

	; adults only: do nothing if anyone in the scene is a child
	Actor[] actors = OThread.GetActors(0)
	Int i = 0
	While i < actors.Length
		If actors[i].IsChild()
			Return
		EndIf
		i += 1
	EndWhile

	; base amount: the highest-value act the player takes part in, in the scene they finished in
	Int playerPos = afIndex as Int
	String pos = playerPos as String
	Int amount = 20                                                        ; vaginal
	Int found = OMetadata.FindActionSuperloadCSVv2(asSceneID, ParticipantPositionsAny = pos, AllActionTags = "intercourse,vaginalpenetration")
	If found < 0
		amount = 15                                                        ; anal
		found = OMetadata.FindActionSuperloadCSVv2(asSceneID, ParticipantPositionsAny = pos, AllActionTags = "intercourse,analpenetration")
	EndIf
	If found < 0
		amount = 10                                                        ; oral
		found = OMetadata.FindActionSuperloadCSVv2(asSceneID, ParticipantPositionsAny = pos, AnyActionTag = "oral", ActionTagBlacklist = "nipplestimulation")
	EndIf
	If found < 0
		amount = 5                                                         ; manual and everything else
		found = OMetadata.FindActionSuperloadCSVv2(asSceneID, ParticipantPositionsAny = pos, AnyActionTag = "sexual")
	EndIf

	; partner bonus: the other side of that act
	If found >= 0
		Int partnerPos = OMetadata.GetActionActor(asSceneID, found)
		If partnerPos == playerPos
			partnerPos = OMetadata.GetActionTarget(asSceneID, found)
		EndIf
		If partnerPos != playerPos
			Actor partner = OThread.GetActor(0, partnerPos)
			If partner != None
				Int rank = partner.GetRelationshipRank(player)
				If rank >= 4
					amount += 10                                           ; spouse / lover
				ElseIf rank >= 2
					amount += 5                                            ; good friend (confidant or ally)
				EndIf
			EndIf
		EndIf
	EndIf

	LowerStress(amount, "You feel the tension drain away.")
EndEvent

; Fertility Mode Reloaded sends FMR_Conception(mother, father, tracking index) when a pregnancy starts.
; The message doesn't mention pregnancy, so it doesn't spoil FMR's own discovery of it.
Event OnFMRConception(Form akMother, Form akFather, Int aiTrackingIndex)
	Actor player = Game.GetPlayer()
	If akMother == player || akFather == player || akFather == player.GetActorBase()
		LowerStress(10, "You feel a deep sense of contentment.")
	EndIf
EndEvent

Function LowerStress(Int aiAmount, String asMessage)
	GlobalVariable stressTotal = Game.GetFormFromFile(0x00000801, "Stress and Fear.esp") as GlobalVariable
	If stressTotal == None
		Return
	EndIf
	Float before = stressTotal.GetValue()
	Float after = before - aiAmount
	If after < 0.0
		after = 0.0
	EndIf
	stressTotal.SetValue(after)
	If before > after
		Debug.Notification(asMessage)
	EndIf
EndFunction
