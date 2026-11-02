// Builds the More Stress Gone patches: one light plugin and one compiled script per patch.
using System.Text;

namespace MsgTool;

public record Patch(string Folder, string PluginName, string QuestEdid, string ScriptName, string Description, Func<PexFile> Script);

public static class Build
{
    public static readonly Patch[] Patches =
    {
        new("GladYoureHere", "MoreStressGone_GladYoureHere.esp", "MoreStressGone_GYHQuest", GYHPatch.ScriptName,
            "More Stress Gone - Glad You're Here: hugs lower Stress and Fear stress. Needs ImGladYoureHere.esp and Stress and Fear.esp.",
            GYHPatch.Script),
        new("OStim", "MoreStressGone_OStim.esp", "MoreStressGone_OStimQuest", OStimPatch.ScriptName,
            "More Stress Gone - OStim: climaxing in OStim scenes (and Fertility Mode Reloaded conceptions) lower Stress and Fear stress. Needs OStim.esp and Stress and Fear.esp.",
            OStimPatch.Script),
    };

    const uint QuestFormId = 0x01000800;           // master index 01 (after Skyrim.esm), ESL range
    public const uint PlayerRef = 0x00000014;

    public static void Run(string[] args)
    {
        switch (args[0])
        {
            case "build": // build <FOMOD package folder>: writes <Folder>/<PluginName> and <Folder>/Scripts/<ScriptName>.pex for every patch
                foreach (var p in Patches)
                {
                    var dir = Path.Combine(args[1], p.Folder);
                    Directory.CreateDirectory(Path.Combine(dir, "Scripts"));
                    var pex = p.Script();
                    Check(pex);
                    File.WriteAllBytes(Path.Combine(dir, "Scripts", p.ScriptName + ".pex"), pex.Write());
                    File.WriteAllBytes(Path.Combine(dir, p.PluginName), Plugin(p).Write());
                    Console.WriteLine("built " + dir);
                }
                break;
            case "verify-gyh": // verify-gyh <FOMOD package folder> <Stress and Fear.esp> <ImGladYoureHere.esp> <WW42HugFollowerScript.pex>
                GYHPatch.Verify(args[1], args[2], args[3], args[4]);
                Finish();
                break;
            case "verify-ostim": // verify-ostim <FOMOD package folder> <Stress and Fear.esp> <OStim mod folder> <FMR Scripts folder>
                OStimPatch.Verify(args[1], args[2], args[3], args[4]);
                Finish();
                break;
            case "ostim-table": // ostim-table <OStim mod folder>: which OStim actions count for how much
                OStimPatch.Table(args[1]);
                break;
            default:
                throw new ArgumentException("unknown command " + args[0]);
        }
    }

    // ---------- tiny assembler: labels resolve to relative jump offsets ----------
    public class Asm
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

    public const byte IADD = 0x01, FSUB = 0x04, NOT = 0x0A, ASSIGN = 0x0D, CAST = 0x0E, CMP_EQ = 0x0F, CMP_LT = 0x10,
        CMP_GT = 0x12, CMP_GE = 0x13, RETURN = 0x1A, ARRAY_LENGTH = 0x1F, ARRAY_GET = 0x20;
    public static V Id(string s) => V.Id(s);

    // Emits: stressTotal = max(0, stressTotal - <amount>), then a notification if it dropped.
    // <amount> is an Int variable. Used by OStim's LowerStress(); the GYH script has its own inline version.
    public static void EmitLowerStress(Asm h, string amountVar, V message)
    {
        h.Static("Game", "GetFormFromFile", "::temp1", V.I(0x00000801), V.S("Stress and Fear.esp"));
        h.Op(CAST, Id("::temp2"), Id("::temp1"));
        h.Op(ASSIGN, Id("stressTotal"), Id("::temp2"));
        h.Op(CAST, Id("::temp2"), V.None);
        h.Op(CMP_EQ, Id("::temp3"), Id("stressTotal"), Id("::temp2"));
        h.JmpF("::temp3", "ls_found");
        h.Op(RETURN, V.None);
        h.Label("ls_found");
        h.Call("GetValue", "stressTotal", "::temp7");
        h.Op(ASSIGN, Id("before"), Id("::temp7"));
        h.Op(CAST, Id("::temp7"), Id(amountVar));
        h.Op(FSUB, Id("::temp7"), Id("before"), Id("::temp7"));
        h.Op(ASSIGN, Id("after"), Id("::temp7"));
        h.Op(CMP_LT, Id("::temp3"), Id("after"), V.F(0f));
        h.JmpF("::temp3", "ls_notNegative");
        h.Op(ASSIGN, Id("after"), V.F(0f));
        h.Label("ls_notNegative");
        h.Call("SetValue", "stressTotal", "::NoneVar", Id("after"));
        h.Op(CMP_GT, Id("::temp3"), Id("before"), Id("after"));
        h.JmpF("::temp3", "ls_end");
        h.Static("Debug", "Notification", "::NoneVar", message);
        h.Label("ls_end");
    }

    // The compiler adds GetState / GotoState to every script.
    public static void AddStateFunctions(PexState st)
    {
        st.Functions.Add(("GetState", new PexFunction { ReturnType = "String", Code = { new Instr(RETURN, Id("::State")) } }));
        var gotoState = new PexFunction { Params = { ("newState", "String") }, Locals = { ("::NoneVar", "None") } };
        gotoState.Code.Add(new Instr(0x17, Id("onEndState"), Id("self"), Id("::NoneVar")));
        gotoState.Code.Add(new Instr(ASSIGN, Id("::State"), Id("newState")));
        gotoState.Code.Add(new Instr(0x17, Id("onBeginState"), Id("self"), Id("::NoneVar")));
        st.Functions.Add(("GotoState", gotoState));
    }

    public static PexFile NewScript(string name, PexObject obj) => new()
    {
        CompileTime = 1793577600, // 2026-11-01, fixed so rebuilds are byte-identical
        Source = name + ".psc",
        User = "Rusted-1",
        Machine = "MsgTool",
        UserFlagDefs = { ("conditional", 1), ("hidden", 0) },
        Objects = { obj }
    };

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
                        int skip = ins.Op switch { 0x17 or 0x18 or 0x1C or 0x1D => 1, 0x19 => 2, _ => 0 }; // method/property, class+function names
                        foreach (var a in ins.Args.Skip(skip).Concat(ins.VarArgs))
                            if (a.T == VT.Ident && !known.Contains((string)a.Val!))
                                throw new Exception($"{name}@{i}: undeclared identifier {a.Val}");
                        // Comparisons: the compiler always casts both sides to one type first (even None),
                        // so both operands must have the same declared type.
                        if (ins.Op is >= CMP_EQ and <= CMP_GE)
                        {
                            var types = ins.Args.Skip(1).Select(a => TypeOf(a, fn, o)).ToList();
                            if (types.Contains(null) || !string.Equals(types[0], types[1], StringComparison.OrdinalIgnoreCase))
                                throw new Exception($"{name}@{i}: comparison of {ins.Args[1]} ({types[0] ?? "None"}) with {ins.Args[2]} ({types[1] ?? "None"}) needs a cast");
                        }
                        if (ins.Op is 0x14 or 0x15 or 0x16)
                        {
                            int target = i + (int)ins.Args[^1].Val!;
                            if (target == i || target < 0 || target > fn.Code.Count) throw new Exception($"{name}@{i}: bad jump to {target}");
                        }
                    }
                }
    }

    // Declared type of an operand; null for a bare None literal.
    static string? TypeOf(V a, PexFunction fn, PexObject o) => a.T switch
    {
        VT.None => null,
        VT.Int => "Int",
        VT.Float => "Float",
        VT.Bool => "Bool",
        VT.Str => "String",
        _ => (string)a.Val! == "self" ? o.Name
            : fn.Params.Concat(fn.Locals).Concat(o.Variables.Select(v => (v.Name, v.Type)))
                .First(p => string.Equals(p.Name, (string)a.Val!, StringComparison.OrdinalIgnoreCase)).Type
    };

    // ---------- verification helpers ----------
    static int fails;
    public static void Ok(bool cond, string what) { Console.WriteLine((cond ? "PASS " : "FAIL ") + what); if (!cond) fails++; }
    static void Finish()
    {
        Console.WriteLine(fails == 0 ? "ALL CHECKS PASSED" : $"{fails} CHECK(S) FAILED");
        if (fails > 0) Environment.Exit(1);
    }

    public static PexFunction Fn(PexFile f, string name) => f.Objects[0].States[0].Functions.First(x => x.Name == name).Fn;

    // Checks shared by every patch: the built files match a fresh build and the plugin has the right shape.
    public static PexFile VerifyCommon(Patch p, string root)
    {
        var dir = Path.Combine(root, p.Folder);
        var espBytes = File.ReadAllBytes(Path.Combine(dir, p.PluginName));
        var pexBytes = File.ReadAllBytes(Path.Combine(dir, "Scripts", p.ScriptName + ".pex"));
        var esp = Esp.Read(espBytes);
        var pex = PexFile.Read(pexBytes);
        Ok(espBytes.SequenceEqual(Plugin(p).Write()), $"{p.PluginName} matches a fresh build");
        Ok(pexBytes.SequenceEqual(p.Script().Write()), $"{p.ScriptName}.pex matches a fresh build");
        Ok(pex.Disassemble() == PexFile.Read(p.Script().Write()).Disassemble(), "script reads back cleanly");
        Ok((esp.Header.Flags & 0x200) != 0, "plugin is ESL-flagged");
        Ok(esp.Header.Subs.Where(s => s.Type == "MAST").Select(s => s.ZStr).SequenceEqual(new[] { "Skyrim.esm" }), "only master is Skyrim.esm");
        var recs = esp.AllRecords().ToList();
        Ok(recs.Count == 1 && recs[0].Type == "QUST" && recs[0].Edid == p.QuestEdid, "one record: QUST " + p.QuestEdid);
        var q = recs[0];
        Ok((q.FormId & 0xFFF) >= 0x800 && (q.FormId >> 24) == 1, "quest FormID is in the light range of this plugin");
        Ok((BitConverter.ToUInt16(q.Get("DNAM")!.Data, 0) & 1) != 0, "quest is Start Game Enabled");
        Ok(BitConverter.ToUInt32(q.Get("ALFR")!.Data, 0) == PlayerRef, "alias 0 is forced to the player");
        var vmad = q.Get("VMAD")!.Data;
        Ok(Encoding.UTF8.GetString(vmad).Contains(p.ScriptName), "alias has " + p.ScriptName + " attached");
        Ok(BitConverter.ToUInt32(vmad, 17) == q.FormId, "alias script entry points at this quest");
        return pex;
    }

    public static void VerifyStressTotal(PexFunction fn, string snfPath)
    {
        var snf = Esp.Read(File.ReadAllBytes(snfPath));
        var call = fn.Code.First(i => i.Op == 0x19 && (string)i.Args[1].Val! == "GetFormFromFile");
        int masters = snf.Header.Subs.Count(s => s.Type == "MAST");
        var rec = snf.AllRecords().FirstOrDefault(r => (r.FormId >> 24) == masters && (r.FormId & 0xFFFFFF) == (uint)(int)call.VarArgs[0].Val!);
        Ok((string)call.VarArgs[1].Val! == "Stress and Fear.esp" && rec?.Type == "GLOB" && rec.Edid == "Stress_Total",
            "form 0x801 in Stress and Fear.esp is GLOB Stress_Total");
    }

    // ---------- plugin ----------
    static byte[] U16(int v) => BitConverter.GetBytes((ushort)v);
    static byte[] U32(uint v) => BitConverter.GetBytes(v);
    static byte[] Cat(params byte[][] parts) => parts.SelectMany(p => p).ToArray();
    static byte[] WStr(string s) { var b = Encoding.UTF8.GetBytes(s); return Cat(U16(b.Length), b); }

    public static Esp Plugin(Patch p)
    {
        var header = new Rec { Type = "TES4", Flags = 0x200 /* ESL */, Version = 44 };
        header.Subs.Add(new Sub("HEDR", Cat(BitConverter.GetBytes(1.71f), U32(2), U32(0x801))));
        header.Subs.Add(new Sub("CNAM", Esp.ZStr("Rusted-1")));
        header.Subs.Add(new Sub("SNAM", Esp.ZStr(p.Description)));
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
            WStr(p.ScriptName), new byte[] { 0 }, U16(0)); // status 0, no properties

        var quest = new Rec { Type = "QUST", FormId = QuestFormId, Version = 44 };
        quest.Subs.Add(new Sub("EDID", Esp.ZStr(p.QuestEdid)));
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
