// Glad You're Here patch: hugs lower stress.
using System.Text;
using static MsgTool.Build;

namespace MsgTool;

public static class GYHPatch
{
    public const string ScriptName = "MoreStressGone_GYHScript";

    // Mirrors GladYoureHere/Source/Scripts/MoreStressGone_GYHScript.psc line by line.
    // Unchanged since v1.0.0, so the compiled script stays byte-identical.
    public static PexFile Script()
    {
        var obj = new PexObject { Name = ScriptName, Parent = "ReferenceAlias" };
        var st = new PexState { Name = "" };
        obj.States.Add(st);

        // Event OnInit() / OnPlayerLoadGame(): RegisterForModEvent("GYH_OnHugActor", "OnGYHHug")
        foreach (var ev in new[] { "OnInit", "OnPlayerLoadGame" })
        {
            var a = new Asm().Local("None", "::NoneVar");
            a.Call("RegisterForModEvent", "self", "::NoneVar", V.S("GYH_OnHugActor"), V.S("OnGYHHug"));
            st.Functions.Add((ev, a.Done()));
        }

        // Event OnGYHHug(Form akSender, Form akTarget)
        var h = new Asm().Param("Form", "akSender").Param("Form", "akTarget")
            .Local("Actor", "::temp0").Local("Form", "::temp1").Local("GlobalVariable", "::temp2")
            .Local("Bool", "::temp3").Local("Bool", "::temp4").Local("Actor", "::temp5").Local("Int", "::temp6")
            .Local("Float", "::temp7").Local("None", "::NoneVar")
            .Local("Actor", "target").Local("GlobalVariable", "stressTotal").Local("Int", "amount")
            .Local("Form", "familyQuest").Local("Float", "before").Local("Float", "after");
        // Actor target = akTarget as Actor
        h.Op(CAST, Id("::temp0"), Id("akTarget"));
        h.Op(ASSIGN, Id("target"), Id("::temp0"));
        // GlobalVariable stressTotal = Game.GetFormFromFile(0x801, "Stress and Fear.esp") as GlobalVariable
        h.Static("Game", "GetFormFromFile", "::temp1", V.I(0x00000801), V.S("Stress and Fear.esp"));
        h.Op(CAST, Id("::temp2"), Id("::temp1"));
        h.Op(ASSIGN, Id("stressTotal"), Id("::temp2"));
        // If target == None || stressTotal == None : Return
        h.Op(CMP_EQ, Id("::temp3"), Id("target"), V.None);
        h.JmpT("::temp3", "or1");
        h.Op(CMP_EQ, Id("::temp4"), Id("stressTotal"), V.None);
        h.Op(ASSIGN, Id("::temp3"), Id("::temp4"));
        h.Label("or1");
        h.JmpF("::temp3", "notMissing");
        h.Op(RETURN, V.None);
        h.Label("notMissing");
        // Int amount = 5
        h.Op(ASSIGN, Id("amount"), V.I(5));
        // Form familyQuest = Game.GetFormFromFile(0x21AF77, "ImGladYoureHere.esp")
        h.Static("Game", "GetFormFromFile", "::temp1", V.I(0x0021AF77), V.S("ImGladYoureHere.esp"));
        h.Op(ASSIGN, Id("familyQuest"), Id("::temp1"));
        // If akSender == familyQuest && target.IsChild() : amount = 25
        h.Op(CMP_EQ, Id("::temp3"), Id("akSender"), Id("familyQuest"));
        h.JmpF("::temp3", "and1");
        h.Call("IsChild", "target", "::temp4");
        h.Op(ASSIGN, Id("::temp3"), Id("::temp4"));
        h.Label("and1");
        h.JmpF("::temp3", "elif1");
        h.Op(ASSIGN, Id("amount"), V.I(25));
        h.Jmp("endif");
        // ElseIf akSender == familyQuest || target.GetRelationshipRank(Game.GetPlayer()) >= 4 : amount = 18
        h.Label("elif1");
        h.Op(CMP_EQ, Id("::temp3"), Id("akSender"), Id("familyQuest"));
        h.JmpT("::temp3", "or2");
        h.Static("Game", "GetPlayer", "::temp5");
        h.Call("GetRelationshipRank", "target", "::temp6", Id("::temp5"));
        h.Op(CMP_GE, Id("::temp4"), Id("::temp6"), V.I(4));
        h.Op(ASSIGN, Id("::temp3"), Id("::temp4"));
        h.Label("or2");
        h.JmpF("::temp3", "elif2");
        h.Op(ASSIGN, Id("amount"), V.I(18));
        h.Jmp("endif");
        // ElseIf target.GetRelationshipRank(Game.GetPlayer()) >= 2 : amount = 10
        h.Label("elif2");
        h.Static("Game", "GetPlayer", "::temp5");
        h.Call("GetRelationshipRank", "target", "::temp6", Id("::temp5"));
        h.Op(CMP_GE, Id("::temp3"), Id("::temp6"), V.I(2));
        h.JmpF("::temp3", "endif");
        h.Op(ASSIGN, Id("amount"), V.I(10));
        h.Label("endif");
        // Float before = stressTotal.GetValue()
        h.Call("GetValue", "stressTotal", "::temp7");
        h.Op(ASSIGN, Id("before"), Id("::temp7"));
        // Float after = before - amount
        h.Op(CAST, Id("::temp7"), Id("amount"));
        h.Op(FSUB, Id("::temp7"), Id("before"), Id("::temp7"));
        h.Op(ASSIGN, Id("after"), Id("::temp7"));
        // If after < 0.0 : after = 0.0
        h.Op(CMP_LT, Id("::temp3"), Id("after"), V.F(0f));
        h.JmpF("::temp3", "notNegative");
        h.Op(ASSIGN, Id("after"), V.F(0f));
        h.Label("notNegative");
        // stressTotal.SetValue(after)
        h.Call("SetValue", "stressTotal", "::NoneVar", Id("after"));
        // If before > after : Debug.Notification(...)
        h.Op(CMP_GT, Id("::temp3"), Id("before"), Id("after"));
        h.JmpF("::temp3", "end");
        h.Static("Debug", "Notification", "::NoneVar", V.S("The hug eases your stress."));
        h.Label("end");
        st.Functions.Add(("OnGYHHug", h.Done()));

        AddStateFunctions(st);
        return NewScript(ScriptName, obj);
    }

    public static void Verify(string root, string snfPath, string gyhPath, string hugPexPath)
    {
        var patch = Patches.First(p => p.ScriptName == ScriptName);
        var pex = VerifyCommon(patch, root);
        var hug = Fn(pex, "OnGYHHug");

        // Script points at the right forms in the original mods.
        VerifyStressTotal(hug, snfPath);
        var gyh = Esp.Read(File.ReadAllBytes(gyhPath));
        var call = hug.Code.Where(i => i.Op == 0x19 && (string)i.Args[1].Val! == "GetFormFromFile").ElementAt(1);
        int gyhMasters = gyh.Header.Subs.Count(s => s.Type == "MAST");
        var fam = gyh.AllRecords().FirstOrDefault(r => (r.FormId >> 24) == gyhMasters && (r.FormId & 0xFFFFFF) == (uint)(int)call.VarArgs[0].Val!);
        Ok((string)call.VarArgs[1].Val! == "ImGladYoureHere.esp" && fam?.Type == "QUST" && fam.Edid == "WW42HugFamily",
            "form 0x21AF77 in ImGladYoureHere.esp is QUST WW42HugFamily");
        var npcProp = Encoding.ASCII.GetBytes("\x03\x00NPC\x03\x01");
        int Npc(Rec r) { var d = r.Get("VMAD")!.Data; int i = d.AsSpan().IndexOf(npcProp); return i < 0 ? -1 : BitConverter.ToInt32(d, i + npcProp.Length); }
        Ok(Npc(fam!) == 1, "WW42HugFamily runs the hug script with NPC = 1 (spouse and children)");
        Ok(Npc(gyh.AllRecords().First(r => r.Edid == "WW42HugFollower")) == 0, "WW42HugFollower runs it with NPC = 0 (everyone else)");

        // GYH's hug script really sends GYH_OnHugActor with (quest, actor).
        var code = Fn(PexFile.Read(File.ReadAllBytes(hugPexPath)), "HugActor").Code;
        int create = code.FindIndex(i => i.Op == 0x19 && (string)i.Args[1].Val! == "Create" && i.VarArgs.Count == 1 && (string)i.VarArgs[0].Val! == "GYH_OnHugActor");
        Ok(create >= 0, "WW42HugFollowerScript.HugActor creates mod event GYH_OnHugActor");
        var pushes = EventPushes(code, create);
        Ok(string.Join(" ", pushes).Equals("PushForm(self) PushForm(akTarget) Send()", StringComparison.OrdinalIgnoreCase),
            "it pushes (self, akTarget) then sends: " + string.Join(" ", pushes));
    }

    // Lists the ModEvent.Push*/Send calls after a ModEvent.Create, following "cast ::tempN x" back to x.
    public static List<string> EventPushes(List<Instr> code, int create)
    {
        var pushes = new List<string>();
        var castFrom = new Dictionary<string, string>();
        foreach (var i in code.Skip(create))
        {
            if (i.Op == CAST) castFrom[i.Args[0].ToString()] = i.Args[1].ToString();
            if (i.Op != 0x19 || !((string)i.Args[1].Val!).StartsWith("Push") && (string)i.Args[1].Val! != "Send") continue;
            var arg = i.VarArgs.Count > 1 ? i.VarArgs[1].ToString() : "";
            pushes.Add((string)i.Args[1].Val! + "(" + castFrom.GetValueOrDefault(arg, arg) + ")");
            if ((string)i.Args[1].Val! == "Send") break;
        }
        return pushes;
    }
}
