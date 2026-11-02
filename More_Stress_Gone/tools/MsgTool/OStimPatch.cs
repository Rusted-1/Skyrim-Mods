// OStim patch: climaxing in an OStim scene, and Fertility Mode Reloaded conceptions, lower stress.
using System.Text.Json;
using System.Text.RegularExpressions;
using static MsgTool.Build;

namespace MsgTool;

public static class OStimPatch
{
    public const string ScriptName = "MoreStressGone_OStimScript";

    // Parameters of OMetadata.FindActionSuperloadCSVv2 after Id, in order. Papyrus calls pass every argument,
    // so the compiled call needs all of them. verify-ostim checks this list against OMetadata.psc.
    public static readonly string[] SuperloadParams =
    {
        "ActorPositions", "TargetPositions", "PerformerPositions", "MatePositionsAny", "MatePositionsAll",
        "ParticipantPositionsAny", "ParticipantPositionsAll", "Types", "AnyActionTag", "AllActionTags",
        "ActionTagWhitelist", "ActionTagBlacklist", "AnyCustomIntRecord", "AllCustomIntRecords",
        "AnyCustomFloatRecord", "allCustomFloatRecords", "anyCustomStringRecord", "allCustomStringRecords",
        "AnyCustomIntListRecord", "AllCustomIntListRecords", "AnyCustomFloatListRecord", "AllCustomFloatListRecords",
        "AnyCustomStringListRecord", "AllCustomStringListRecords"
    };

    // The act tiers, highest first: (amount, AnyActionTag, AllActionTags, ActionTagBlacklist). Mirrors the .psc.
    public static readonly (int Amount, string AnyTag, string AllTags, string Blacklist)[] Tiers =
    {
        (20, "", "intercourse,vaginalpenetration", ""),
        (15, "", "intercourse,analpenetration", ""),
        (10, "oral", "", "nipplestimulation"),  // nipple licking/sucking count as 5
        (5, "sexual", "", ""),
    };

    static V[] SuperloadArgs(string sceneVar, string posVar, (int Amount, string AnyTag, string AllTags, string Blacklist) tier)
    {
        var args = new List<V> { Id(sceneVar) };
        foreach (var p in SuperloadParams)
            args.Add(p switch
            {
                "ParticipantPositionsAny" => Id(posVar),
                "AnyActionTag" => V.S(tier.AnyTag),
                "AllActionTags" => V.S(tier.AllTags),
                "ActionTagBlacklist" => V.S(tier.Blacklist),
                _ => V.S("")
            });
        return args.ToArray();
    }

    // Mirrors OStim/Source/Scripts/MoreStressGone_OStimScript.psc line by line.
    public static PexFile Script()
    {
        var obj = new PexObject { Name = ScriptName, Parent = "ReferenceAlias" };
        var st = new PexState { Name = "" };
        obj.States.Add(st);

        // Event OnInit() / OnPlayerLoadGame(): RegisterEvents()
        foreach (var ev in new[] { "OnInit", "OnPlayerLoadGame" })
        {
            var a = new Asm().Local("None", "::NoneVar");
            a.Call("RegisterEvents", "self", "::NoneVar");
            st.Functions.Add((ev, a.Done()));
        }

        // Function RegisterEvents()
        var reg = new Asm().Local("None", "::NoneVar");
        reg.Call("RegisterForModEvent", "self", "::NoneVar", V.S("ostim_orgasm"), V.S("OnOStimOrgasm"));
        reg.Call("RegisterForModEvent", "self", "::NoneVar", V.S("FMR_Conception"), V.S("OnFMRConception"));
        st.Functions.Add(("RegisterEvents", reg.Done()));

        // Event OnOStimOrgasm(String asEventName, String asSceneID, Float afIndex, Form akSender)
        var h = new Asm().Param("String", "asEventName").Param("String", "asSceneID").Param("Float", "afIndex").Param("Form", "akSender")
            .Local("Actor", "::temp0").Local("Actor[]", "::temp1").Local("Bool", "::temp2").Local("Bool", "::temp3")
            .Local("Int", "::temp4").Local("String", "::temp5").Local("None", "::NoneVar")
            .Local("Actor", "player").Local("Actor[]", "actors").Local("Int", "i").Local("Int", "playerPos")
            .Local("String", "pos").Local("Int", "amount").Local("Int", "found").Local("Int", "partnerPos")
            .Local("Actor", "partner").Local("Int", "rank");
        // Actor player = Game.GetPlayer() / If akSender != player : Return
        h.Static("Game", "GetPlayer", "::temp0");
        h.Op(ASSIGN, Id("player"), Id("::temp0"));
        h.Op(CMP_EQ, Id("::temp2"), Id("akSender"), Id("player"));
        h.Op(NOT, Id("::temp2"), Id("::temp2"));
        h.JmpF("::temp2", "isPlayer");
        h.Op(RETURN, V.None);
        h.Label("isPlayer");
        // Actor[] actors = OThread.GetActors(0) / i = 0
        h.Static("OThread", "GetActors", "::temp1", V.I(0));
        h.Op(ASSIGN, Id("actors"), Id("::temp1"));
        h.Op(ASSIGN, Id("i"), V.I(0));
        // While i < actors.Length : If actors[i].IsChild() : Return : i += 1
        h.Label("loop");
        h.Op(ARRAY_LENGTH, Id("::temp4"), Id("actors"));
        h.Op(CMP_LT, Id("::temp2"), Id("i"), Id("::temp4"));
        h.JmpF("::temp2", "loopEnd");
        h.Op(ARRAY_GET, Id("::temp0"), Id("actors"), Id("i"));
        h.Call("IsChild", "::temp0", "::temp3");
        h.JmpF("::temp3", "notChild");
        h.Op(RETURN, V.None);
        h.Label("notChild");
        h.Op(IADD, Id("i"), Id("i"), V.I(1));
        h.Jmp("loop");
        h.Label("loopEnd");
        // Int playerPos = afIndex as Int / String pos = playerPos as String
        h.Op(CAST, Id("::temp4"), Id("afIndex"));
        h.Op(ASSIGN, Id("playerPos"), Id("::temp4"));
        h.Op(CAST, Id("::temp5"), Id("playerPos"));
        h.Op(ASSIGN, Id("pos"), Id("::temp5"));
        // amount = 20 / found = FindActionSuperloadCSVv2(vaginal)
        h.Op(ASSIGN, Id("amount"), V.I(Tiers[0].Amount));
        h.Static("OMetadata", "FindActionSuperloadCSVv2", "::temp4", SuperloadArgs("asSceneID", "pos", Tiers[0]));
        h.Op(ASSIGN, Id("found"), Id("::temp4"));
        // If found < 0 : amount = next tier / found = FindActionSuperloadCSVv2(next tier)   (anal, oral, sexual)
        for (int t = 1; t < Tiers.Length; t++)
        {
            h.Op(CMP_LT, Id("::temp2"), Id("found"), V.I(0));
            h.JmpF("::temp2", "tier" + t);
            h.Op(ASSIGN, Id("amount"), V.I(Tiers[t].Amount));
            h.Static("OMetadata", "FindActionSuperloadCSVv2", "::temp4", SuperloadArgs("asSceneID", "pos", Tiers[t]));
            h.Op(ASSIGN, Id("found"), Id("::temp4"));
            h.Label("tier" + t);
        }
        // If found >= 0
        h.Op(CMP_GE, Id("::temp2"), Id("found"), V.I(0));
        h.JmpF("::temp2", "bonusDone");
        //   Int partnerPos = OMetadata.GetActionActor(asSceneID, found)
        h.Static("OMetadata", "GetActionActor", "::temp4", Id("asSceneID"), Id("found"));
        h.Op(ASSIGN, Id("partnerPos"), Id("::temp4"));
        //   If partnerPos == playerPos : partnerPos = OMetadata.GetActionTarget(asSceneID, found)
        h.Op(CMP_EQ, Id("::temp2"), Id("partnerPos"), Id("playerPos"));
        h.JmpF("::temp2", "partnerKnown");
        h.Static("OMetadata", "GetActionTarget", "::temp4", Id("asSceneID"), Id("found"));
        h.Op(ASSIGN, Id("partnerPos"), Id("::temp4"));
        h.Label("partnerKnown");
        //   If partnerPos != playerPos
        h.Op(CMP_EQ, Id("::temp2"), Id("partnerPos"), Id("playerPos"));
        h.Op(NOT, Id("::temp2"), Id("::temp2"));
        h.JmpF("::temp2", "bonusDone");
        //     Actor partner = OThread.GetActor(0, partnerPos) / If partner != None
        h.Static("OThread", "GetActor", "::temp0", V.I(0), Id("partnerPos"));
        h.Op(ASSIGN, Id("partner"), Id("::temp0"));
        h.Op(CMP_EQ, Id("::temp2"), Id("partner"), V.None);
        h.Op(NOT, Id("::temp2"), Id("::temp2"));
        h.JmpF("::temp2", "bonusDone");
        //       Int rank = partner.GetRelationshipRank(player)
        h.Call("GetRelationshipRank", "partner", "::temp4", Id("player"));
        h.Op(ASSIGN, Id("rank"), Id("::temp4"));
        //       If rank >= 4 : amount += 10 / ElseIf rank >= 2 : amount += 5
        h.Op(CMP_GE, Id("::temp2"), Id("rank"), V.I(4));
        h.JmpF("::temp2", "notLover");
        h.Op(IADD, Id("amount"), Id("amount"), V.I(10));
        h.Jmp("bonusDone");
        h.Label("notLover");
        h.Op(CMP_GE, Id("::temp2"), Id("rank"), V.I(2));
        h.JmpF("::temp2", "bonusDone");
        h.Op(IADD, Id("amount"), Id("amount"), V.I(5));
        h.Label("bonusDone");
        // LowerStress(amount, "You feel the tension drain away.")
        h.Call("LowerStress", "self", "::NoneVar", Id("amount"), V.S("You feel the tension drain away."));
        st.Functions.Add(("OnOStimOrgasm", h.Done()));

        // Event OnFMRConception(Form akMother, Form akFather, Int aiTrackingIndex)
        var c = new Asm().Param("Form", "akMother").Param("Form", "akFather").Param("Int", "aiTrackingIndex")
            .Local("Actor", "::temp0").Local("Bool", "::temp1").Local("ActorBase", "::temp2").Local("None", "::NoneVar")
            .Local("Actor", "player");
        c.Static("Game", "GetPlayer", "::temp0");
        c.Op(ASSIGN, Id("player"), Id("::temp0"));
        // If akMother == player || akFather == player || akFather == player.GetActorBase()
        c.Op(CMP_EQ, Id("::temp1"), Id("akMother"), Id("player"));
        c.JmpT("::temp1", "check");
        c.Op(CMP_EQ, Id("::temp1"), Id("akFather"), Id("player"));
        c.JmpT("::temp1", "check");
        c.Call("GetActorBase", "player", "::temp2");
        c.Op(CMP_EQ, Id("::temp1"), Id("akFather"), Id("::temp2"));
        c.Label("check");
        c.JmpF("::temp1", "end");
        c.Call("LowerStress", "self", "::NoneVar", V.I(10), V.S("You feel a deep sense of contentment."));
        c.Label("end");
        st.Functions.Add(("OnFMRConception", c.Done()));

        // Function LowerStress(Int aiAmount, String asMessage)
        var l = new Asm().Param("Int", "aiAmount").Param("String", "asMessage")
            .Local("Form", "::temp1").Local("GlobalVariable", "::temp2").Local("Bool", "::temp3").Local("Float", "::temp7")
            .Local("None", "::NoneVar").Local("GlobalVariable", "stressTotal").Local("Float", "before").Local("Float", "after");
        EmitLowerStress(l, "aiAmount", Id("asMessage"));
        st.Functions.Add(("LowerStress", l.Done()));

        AddStateFunctions(st);
        return NewScript(ScriptName, obj);
    }

    // Which tier an action with these tags lands in (same rules as the script), or 0 if none.
    public static int TierFor(HashSet<string> tags) =>
        Tiers.FirstOrDefault(t =>
            (t.AnyTag == "" || t.AnyTag.Split(',').Any(tags.Contains)) &&
            (t.AllTags == "" || t.AllTags.Split(',').All(tags.Contains)) &&
            (t.Blacklist == "" || !t.Blacklist.Split(',').Any(tags.Contains))).Amount;

    public static Dictionary<string, HashSet<string>> ReadActionTags(string ostimRoot)
    {
        var result = new Dictionary<string, HashSet<string>>();
        foreach (var f in Directory.GetFiles(Path.Combine(ostimRoot, "SKSE", "Plugins", "OStim", "actions"), "*.json"))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(f), new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            var tags = new HashSet<string>();
            if (doc.RootElement.TryGetProperty("tags", out var t))
                foreach (var e in t.EnumerateArray()) tags.Add(e.GetString()!.ToLowerInvariant());
            result[Path.GetFileNameWithoutExtension(f).ToLowerInvariant()] = tags;
        }
        return result;
    }

    public static void Verify(string root, string snfPath, string ostimRoot, string fmrScripts)
    {
        var patch = Patches.First(p => p.ScriptName == ScriptName);
        var pex = VerifyCommon(patch, root);
        VerifyStressTotal(Fn(pex, "LowerStress"), snfPath);

        // OStim API: the natives the script calls exist with the signatures it assumes.
        var src = Path.Combine(ostimRoot, "Scripts", "Source");
        var meta = File.ReadAllText(Path.Combine(src, "OMetadata.psc"));
        var thread = File.ReadAllText(Path.Combine(src, "OThread.psc"));
        var sig = Regex.Match(meta, @"int Function FindActionSuperloadCSVv2\(string Id, ([^)]*)\) Global Native");
        var names = sig.Success ? sig.Groups[1].Value.Split(',').Select(s => s.Trim().Split(' ')[1]).ToArray() : Array.Empty<string>();
        Ok(names.SequenceEqual(SuperloadParams), $"OMetadata.FindActionSuperloadCSVv2 takes Id + the {SuperloadParams.Length} string parameters the script passes, in order");
        Ok(meta.Contains("int Function GetActionActor(string Id, int Index) Global Native"), "OMetadata.GetActionActor(string, int) exists");
        Ok(meta.Contains("int Function GetActionTarget(string Id, int Index) Global Native"), "OMetadata.GetActionTarget(string, int) exists");
        Ok(thread.Contains("Actor[] Function GetActors(int ThreadID) Global Native"), "OThread.GetActors(int) exists");
        Ok(thread.Contains("Actor Function GetActor(int ThreadID, int Index) Global Native"), "OThread.GetActor(int, int) exists");
        var events = File.ReadAllText(Path.Combine(ostimRoot, "SKSE", "Plugins", "OStim", "list of mod events.txt"));
        Ok(events.Contains("Event OStimOrgasm(string EventName, string SceneID, float Index, Form Sender)")
           && events.Contains("these events only fire on the main thread"), "ostim_orgasm is a main-thread event with (SceneID, actor index, actor)");

        // Every OStim action lands in the tier the user asked for.
        var tags = ReadActionTags(ostimRoot);
        var expect = new Dictionary<string, int>
        {
            ["handjob"] = 5, ["footjob"] = 5, ["boobjob"] = 5, ["vaginalfingering"] = 5, ["malemasturbation"] = 5,
            ["blowjob"] = 10, ["deepthroating"] = 10, ["vulvaleating"] = 10, ["rimjob"] = 10, ["lickingnipple"] = 5, ["suckingnipple"] = 5,
            ["analsex"] = 15, ["vaginalsex"] = 20, ["kissing"] = 0, ["cuddling"] = 0,
        };
        foreach (var (action, amount) in expect)
            Ok(tags.ContainsKey(action) && TierFor(tags[action]) == amount, $"action '{action}' counts as {amount}");

        // FMR sends FMR_Conception with (mother, father, tracking index).
        var util = PexFile.Read(File.ReadAllBytes(Path.Combine(fmrScripts, "_JSW_BB_Utility.pex")));
        var send = Fn(util, "SendConceptionEvent");
        var fnParams = string.Join(", ", send.Params.Select(p => p.Type + " " + p.Name));
        int create = send.Code.FindIndex(i => i.Op == 0x19 && (string)i.Args[1].Val! == "Create" && i.VarArgs.Count == 1 && (string)i.VarArgs[0].Val! == "FMR_Conception");
        var pushes = create < 0 ? new List<string>() : GYHPatch.EventPushes(send.Code, create);
        Ok(fnParams == "Actor akMother, Form akFather, Int iTrackingIndex" && create >= 0, "FMR's SendConceptionEvent(Actor akMother, Form akFather, Int iTrackingIndex) creates FMR_Conception");
        Ok(string.Join(" ", pushes) == "PushForm(akMother) PushForm(akFather) PushInt(iTrackingIndex) Send()",
            "it pushes (mother, father, index) then sends: " + string.Join(" ", pushes));
    }

    // Prints every OStim action that can count, with its amount, for the docs.
    public static void Table(string ostimRoot)
    {
        foreach (var g in ReadActionTags(ostimRoot).Select(kv => (kv.Key, Amount: TierFor(kv.Value))).Where(x => x.Amount > 0)
                     .GroupBy(x => x.Amount).OrderByDescending(g => g.Key))
            Console.WriteLine($"| {g.Key} | {string.Join(", ", g.Select(x => x.Key).OrderBy(x => x))} |");
    }
}
