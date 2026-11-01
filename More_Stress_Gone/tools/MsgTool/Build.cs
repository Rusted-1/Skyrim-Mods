// Builds the More Stress Gone - Glad You're Here patch: the plugin and the compiled script.
using System.Text;

namespace MsgTool;

public static class Build
{
    public const string ScriptName = "MoreStressGone_GYHScript";
    public const string QuestEdid = "MoreStressGone_GYHQuest";
    const uint QuestFormId = 0x01000800;           // master index 01 (after Skyrim.esm), ESL range
    const uint PlayerRef = 0x00000014;

    public static void Run(string[] args)
    {
        switch (args[0])
        {
            case "build": // build <outdir>: writes <outdir>/MoreStressGone_GladYoureHere.esp and Scripts/<ScriptName>.pex
                var outDir = args[1];
                Directory.CreateDirectory(Path.Combine(outDir, "Scripts"));
                var pex = GYHScript();
                Check(pex);
                File.WriteAllBytes(Path.Combine(outDir, "Scripts", ScriptName + ".pex"), pex.Write());
                File.WriteAllBytes(Path.Combine(outDir, "MoreStressGone_GladYoureHere.esp"), Plugin().Write());
                Console.WriteLine("built " + outDir);
                break;
            case "verify": // verify <outdir> <Stress and Fear.esp> <ImGladYoureHere.esp> <WW42HugFollowerScript.pex>
                Verify(args[1], args[2], args[3], args[4]);
                break;
            default:
                throw new ArgumentException("unknown command " + args[0]);
        }
    }

    // ---------- tiny assembler: labels resolve to relative jump offsets ----------
    class Asm
    {
        public readonly PexFunction Fn = new();
        readonly Dictionary<string, int> labels = new();
        readonly List<(int At, string Label, int ArgIdx)> fixups = new();
        public Asm Local(string type, string name) { Fn.Locals.Add((name, type)); return this; }
        public Asm Param(string type, string name) { Fn.Params.Add((name, type)); return this; }
        public void Op(byte op, params V[] a) => Fn.Code.Add(new Instr(op, a));
        public void Call(string method, string obj, string dest, params V[] a) { var i = new Instr(0x17, V.Id(method), V.Id(obj), V.Id(dest)); i.VarArgs.AddRange(a); Fn.Code.Add(i); }
        public void Static(string cls, string fn, string dest, params V[] a) { var i = new Instr(0x19, V.Id(cls), V.Id(fn), V.Id(dest)); i.VarArgs.AddRange(a); Fn.Code.Add(i); }
        public void Label(string l) => labels[l] = Fn.Code.Count;
        public void Jmp(string l) { fixups.Add((Fn.Code.Count, l, 0)); Op(0x14, V.I(0)); }
        public void JmpT(string cond, string l) { fixups.Add((Fn.Code.Count, l, 1)); Op(0x15, V.Id(cond), V.I(0)); }
        public void JmpF(string cond, string l) { fixups.Add((Fn.Code.Count, l, 1)); Op(0x16, V.Id(cond), V.I(0)); }
        public PexFunction Done()
        {
            foreach (var (at, l, ai) in fixups) Fn.Code[at].Args[ai] = V.I(labels[l] - at);
            return Fn;
        }
    }

    static V Id(string s) => V.Id(s);
    const byte ASSIGN = 0x0D, CAST = 0x0E, FSUB = 0x04, CMP_EQ = 0x0F, CMP_LT = 0x10, CMP_GT = 0x12, CMP_GE = 0x13, RETURN = 0x1A;

    // Mirrors build/src/MoreStressGone_GYHScript.psc line by line.
    public static PexFile GYHScript()
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

        // GetState / GotoState: the compiler adds these to every script.
        st.Functions.Add(("GetState", new PexFunction { ReturnType = "String", Code = { new Instr(RETURN, Id("::State")) } }));
        var gotoState = new PexFunction { Params = { ("newState", "String") }, Locals = { ("::NoneVar", "None") } };
        gotoState.Code.Add(new Instr(0x17, Id("onEndState"), Id("self"), Id("::NoneVar")));
        gotoState.Code.Add(new Instr(ASSIGN, Id("::State"), Id("newState")));
        gotoState.Code.Add(new Instr(0x17, Id("onBeginState"), Id("self"), Id("::NoneVar")));
        st.Functions.Add(("GotoState", gotoState));

        return new PexFile
        {
            CompileTime = 1793577600, // 2026-11-01, fixed so rebuilds are byte-identical
            Source = ScriptName + ".psc",
            User = "Rusted-1",
            Machine = "MsgTool",
            UserFlagDefs = { ("conditional", 1), ("hidden", 0) },
            Objects = { obj }
        };
    }

    // Sanity checks on hand-assembled code: every identifier is declared, jumps stay inside the function.
    public static void Check(PexFile f)
    {
        foreach (var o in f.Objects)
            foreach (var st in o.States)
                foreach (var (name, fn) in st.Functions)
                {
                    var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "self", "::State" };
                    foreach (var p in fn.Params) known.Add(p.Name);
                    foreach (var l in fn.Locals) known.Add(l.Name);
                    foreach (var v in o.Variables) known.Add(v.Name);
                    for (int i = 0; i < fn.Code.Count; i++)
                    {
                        var ins = fn.Code[i];
                        int skip = ins.Op switch { 0x17 => 1, 0x19 => 2, _ => 0 }; // method / class+function names
                        foreach (var a in ins.Args.Skip(skip).Concat(ins.VarArgs))
                            if (a.T == VT.Ident && !known.Contains((string)a.Val!))
                                throw new Exception($"{name}@{i}: undeclared identifier {a.Val}");
                        if (ins.Op is 0x14 or 0x15 or 0x16)
                        {
                            int target = i + (int)ins.Args[^1].Val!;
                            if (target <= i || target > fn.Code.Count) throw new Exception($"{name}@{i}: bad jump to {target}");
                        }
                    }
                }
    }

    // ---------- verification against the original mods ----------
    static int fails;
    static void Ok(bool cond, string what) { Console.WriteLine((cond ? "PASS " : "FAIL ") + what); if (!cond) fails++; }

    static void Verify(string outDir, string snfPath, string gyhPath, string hugPexPath)
    {
        var espBytes = File.ReadAllBytes(Path.Combine(outDir, "MoreStressGone_GladYoureHere.esp"));
        var pexBytes = File.ReadAllBytes(Path.Combine(outDir, "Scripts", ScriptName + ".pex"));

        // 1. Our files read back and are exactly what the builder makes now.
        var esp = Esp.Read(espBytes);
        var pex = PexFile.Read(pexBytes);
        Ok(espBytes.SequenceEqual(Plugin().Write()), "plugin matches a fresh build");
        Ok(pexBytes.SequenceEqual(GYHScript().Write()), "script matches a fresh build");
        Ok(pex.Disassemble() == PexFile.Read(GYHScript().Write()).Disassemble(), "script reads back cleanly");

        // 2. Plugin shape.
        Ok((esp.Header.Flags & 0x200) != 0, "plugin is ESL-flagged");
        Ok(esp.Header.Subs.Where(s => s.Type == "MAST").Select(s => s.ZStr).SequenceEqual(new[] { "Skyrim.esm" }), "only master is Skyrim.esm");
        var recs = esp.AllRecords().ToList();
        Ok(recs.Count == 1 && recs[0].Type == "QUST" && recs[0].Edid == QuestEdid, "one record: QUST " + QuestEdid);
        var q = recs[0];
        Ok((q.FormId & 0xFFF) >= 0x800 && (q.FormId >> 24) == 1, "quest FormID is in the light range of this plugin");
        Ok((BitConverter.ToUInt16(q.Get("DNAM")!.Data, 0) & 1) != 0, "quest is Start Game Enabled");
        Ok(BitConverter.ToUInt32(q.Get("ALFR")!.Data, 0) == PlayerRef, "alias 0 is forced to the player");
        var vmad = q.Get("VMAD")!.Data;
        Ok(Encoding.UTF8.GetString(vmad).Contains(ScriptName), "alias has " + ScriptName + " attached");
        Ok(BitConverter.ToUInt32(vmad, 17) == q.FormId, "alias script entry points at this quest");

        // 3. Script points at the right forms in the original mods.
        var snf = Esp.Read(File.ReadAllBytes(snfPath));
        var gyh = Esp.Read(File.ReadAllBytes(gyhPath));
        var calls = pex.Objects[0].States[0].Functions.First(f => f.Name == "OnGYHHug").Fn.Code
            .Where(i => i.Op == 0x19 && (string)i.Args[1].Val! == "GetFormFromFile").ToList();
        int snfMasters = snf.Header.Subs.Count(s => s.Type == "MAST"), gyhMasters = gyh.Header.Subs.Count(s => s.Type == "MAST");
        var stress = snf.AllRecords().FirstOrDefault(r => (r.FormId >> 24) == snfMasters && (r.FormId & 0xFFFFFF) == (uint)(int)calls[0].VarArgs[0].Val!);
        Ok((string)calls[0].VarArgs[1].Val! == "Stress and Fear.esp" && stress?.Type == "GLOB" && stress.Edid == "Stress_Total",
            "form 0x801 in Stress and Fear.esp is GLOB Stress_Total");
        var fam = gyh.AllRecords().FirstOrDefault(r => (r.FormId >> 24) == gyhMasters && (r.FormId & 0xFFFFFF) == (uint)(int)calls[1].VarArgs[0].Val!);
        Ok((string)calls[1].VarArgs[1].Val! == "ImGladYoureHere.esp" && fam?.Type == "QUST" && fam.Edid == "WW42HugFamily",
            "form 0x21AF77 in ImGladYoureHere.esp is QUST WW42HugFamily");
        var npcProp = Encoding.ASCII.GetBytes("\x03\x00NPC\x03\x01");
        int Npc(Rec r) { var d = r.Get("VMAD")!.Data; int i = d.AsSpan().IndexOf(npcProp); return i < 0 ? -1 : BitConverter.ToInt32(d, i + npcProp.Length); }
        Ok(Npc(fam!) == 1, "WW42HugFamily runs the hug script with NPC = 1 (spouse and children)");
        Ok(Npc(gyh.AllRecords().First(r => r.Edid == "WW42HugFollower")) == 0, "WW42HugFollower runs it with NPC = 0 (everyone else)");

        // 4. GYH's hug script really sends GYH_OnHugActor with (quest, actor).
        var hug = PexFile.Read(File.ReadAllBytes(hugPexPath)).Objects[0].States[0].Functions.First(f => f.Name == "HugActor").Fn.Code;
        int create = hug.FindIndex(i => i.Op == 0x19 && (string)i.Args[1].Val! == "Create" && i.VarArgs.Count == 1 && (string)i.VarArgs[0].Val! == "GYH_OnHugActor");
        // The compiler casts each argument into a temp first ("cast ::temp12 self"), so follow the casts back.
        var pushes = new List<string>();
        var castFrom = new Dictionary<string, string>();
        foreach (var i in hug.Skip(create))
        {
            if (i.Op == 0x0E) castFrom[i.Args[0].ToString()] = i.Args[1].ToString();
            if (i.Op != 0x19 || (string)i.Args[1].Val! is not ("PushForm" or "Send")) continue;
            var arg = i.VarArgs.Count > 1 ? i.VarArgs[1].ToString() : "";
            pushes.Add((string)i.Args[1].Val! + "(" + castFrom.GetValueOrDefault(arg, arg) + ")");
            if (pushes.Count == 3) break;
        }
        Ok(create >= 0, "WW42HugFollowerScript.HugActor creates mod event GYH_OnHugActor");
        Ok(string.Join(" ", pushes).Equals("PushForm(self) PushForm(akTarget) Send()", StringComparison.OrdinalIgnoreCase),
            "it pushes (self, akTarget) then sends: " + string.Join(" ", pushes));

        Console.WriteLine(fails == 0 ? "ALL CHECKS PASSED" : $"{fails} CHECK(S) FAILED");
        if (fails > 0) Environment.Exit(1);
    }

    // ---------- plugin ----------
    static byte[] U16(int v) => BitConverter.GetBytes((ushort)v);
    static byte[] U32(uint v) => BitConverter.GetBytes(v);
    static byte[] Cat(params byte[][] parts) => parts.SelectMany(p => p).ToArray();
    static byte[] WStr(string s) { var b = Encoding.UTF8.GetBytes(s); return Cat(U16(b.Length), b); }

    public static Esp Plugin()
    {
        var header = new Rec { Type = "TES4", Flags = 0x200 /* ESL */, Version = 44 };
        header.Subs.Add(new Sub("HEDR", Cat(BitConverter.GetBytes(1.71f), U32(2), U32(0x801))));
        header.Subs.Add(new Sub("CNAM", Esp.ZStr("Rusted-1")));
        header.Subs.Add(new Sub("SNAM", Esp.ZStr("More Stress Gone - Glad You're Here: hugs lower Stress and Fear stress. Needs ImGladYoureHere.esp and Stress and Fear.esp.")));
        header.Subs.Add(new Sub("MAST", Esp.ZStr("Skyrim.esm")));
        header.Subs.Add(new Sub("DATA", new byte[8]));
        header.Subs.Add(new Sub("INTV", U32(1)));

        // VMAD: no quest scripts, empty fragment block, one alias (0) with our script. Layout copied from Stress_Quest.
        var vmad = Cat(
            U16(5), U16(2), U16(0),                 // version 5, object format 2, 0 quest scripts
            new byte[] { 2 }, U16(0), WStr(""),     // fragment block: version 2, 0 fragments, no file name
            U16(1),                                 // 1 alias
            U16(0), U16(0), U32(QuestFormId),       // alias object: unused, alias id 0, owning quest
            U16(5), U16(2), U16(1),                 // alias scripts: version 5, format 2, 1 script
            WStr(ScriptName), new byte[] { 0 }, U16(0)); // status 0, no properties

        var quest = new Rec { Type = "QUST", FormId = QuestFormId, Version = 44 };
        quest.Subs.Add(new Sub("EDID", Esp.ZStr(QuestEdid)));
        quest.Subs.Add(new Sub("VMAD", vmad));
        quest.Subs.Add(new Sub("DNAM", new byte[] { 0x11, 0x00, 0x32, 0xFF, 0, 0, 0, 0, 0, 0, 0, 0 })); // start game enabled + run once, priority 50 (same as Stress_Quest)
        quest.Subs.Add(new Sub("NEXT", Array.Empty<byte>()));
        quest.Subs.Add(new Sub("ANAM", U32(1)));    // next alias id
        quest.Subs.Add(new Sub("ALST", U32(0)));    // alias 0
        quest.Subs.Add(new Sub("ALID", Esp.ZStr("Player")));
        quest.Subs.Add(new Sub("FNAM", U32(0)));
        quest.Subs.Add(new Sub("ALFR", U32(PlayerRef))); // forced reference: the player
        quest.Subs.Add(new Sub("VTCK", U32(0)));
        quest.Subs.Add(new Sub("ALED", Array.Empty<byte>()));

        var grp = new Grp { Label = "QUST", LabelRaw = BitConverter.ToUInt32(Encoding.ASCII.GetBytes("QUST")), GroupType = 0 };
        grp.Items.Add(quest);
        return new Esp { Header = header, Groups = { grp } };
    }
}
