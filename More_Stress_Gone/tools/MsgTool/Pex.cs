// Skyrim SE compiled Papyrus (.pex) reader, writer and disassembler.
// Format: big-endian, version 3.2, game id 1.
using System.Text;

namespace MsgTool;

public enum VT : byte { None = 0, Ident = 1, Str = 2, Int = 3, Float = 4, Bool = 5 }

public record V(VT T, object? Val)
{
    public static V None => new(VT.None, null);
    public static V Id(string s) => new(VT.Ident, s);
    public static V S(string s) => new(VT.Str, s);
    public static V I(int i) => new(VT.Int, i);
    public static V F(float f) => new(VT.Float, f);
    public static V B(bool b) => new(VT.Bool, b);
    public override string ToString() => T switch
    {
        VT.None => "None",
        VT.Ident => (string)Val!,
        VT.Str => "\"" + Val + "\"",
        VT.Int => Val!.ToString()!,
        VT.Float => ((float)Val!).ToString("R") + "f",
        VT.Bool => (bool)Val! ? "True" : "False",
        _ => "?"
    };
}

public class Instr
{
    public byte Op;
    public List<V> Args = new();      // fixed args
    public List<V> VarArgs = new();   // variadic args (call ops)
    public Instr(byte op, params V[] args) { Op = op; Args.AddRange(args); }
}

public class PexFunction
{
    public string ReturnType = "None", Doc = "";
    public uint UserFlags;
    public byte Flags; // 1 global, 2 native
    public List<(string Name, string Type)> Params = new(), Locals = new();
    public List<Instr> Code = new();
}

public class PexProperty
{
    public string Name = "", Type = "", Doc = "", AutoVar = "";
    public uint UserFlags;
    public byte Flags; // 1 read, 2 write, 4 auto
    public PexFunction? Read, Write;
}

public class PexVariable
{
    public string Name = "", Type = "";
    public uint UserFlags;
    public V Value = V.None;
}

public class PexState
{
    public string Name = "";
    public List<(string Name, PexFunction Fn)> Functions = new();
}

public class PexObject
{
    public string Name = "", Parent = "", Doc = "", AutoState = "";
    public uint UserFlags;
    public List<PexVariable> Variables = new();
    public List<PexProperty> Properties = new();
    public List<PexState> States = new();
    public uint SizeOnDisk;
}

public class PexFile
{
    public byte Major = 3, Minor = 2;
    public ushort GameId = 1;
    public ulong CompileTime;
    public string Source = "", User = "", Machine = "";
    public List<(string Name, byte Index)> UserFlagDefs = new();
    public List<PexObject> Objects = new();
    public bool HadDebugInfo;

    public static readonly string[] OpNames = {
        "nop","iadd","fadd","isub","fsub","imul","fmul","idiv","fdiv","imod","not","ineg","fneg","assign","cast",
        "cmp_eq","cmp_lt","cmp_le","cmp_gt","cmp_ge","jmp","jmpt","jmpf","callmethod","callparent","callstatic",
        "return","strcat","propget","propset","array_create","array_length","array_getelement","array_setelement",
        "array_findelement","array_rfindelement" };
    static readonly int[] FixedArgs = { 0,3,3,3,3,3,3,3,3,3,2,2,2,2,2,3,3,3,3,3,1,2,2,3,2,3,1,3,3,3,2,2,3,3,4,4 };
    static bool IsVariadic(byte op) => op is 0x17 or 0x18 or 0x19;

    // ---------- reading ----------
    class R
    {
        readonly byte[] b; public int P;
        public List<string> Str = new();
        public R(byte[] d) { b = d; }
        public byte U8() => b[P++];
        public ushort U16() { var v = (ushort)(b[P] << 8 | b[P + 1]); P += 2; return v; }
        public uint U32() { var v = (uint)(b[P] << 24 | b[P + 1] << 16 | b[P + 2] << 8 | b[P + 3]); P += 4; return v; }
        public ulong U64() { ulong hi = U32(); return hi << 32 | U32(); }
        public string WStr() { int n = U16(); var s = Encoding.UTF8.GetString(b, P, n); P += n; return s; }
        public string S() => Str[U16()];
        public V Val()
        {
            var t = (VT)U8();
            return t switch
            {
                VT.None => V.None,
                VT.Ident => V.Id(S()),
                VT.Str => V.S(S()),
                VT.Int => V.I((int)U32()),
                VT.Float => V.F(BitConverter.Int32BitsToSingle((int)U32())),
                VT.Bool => V.B(U8() != 0),
                _ => throw new InvalidDataException($"bad value type {t} at {P - 1}")
            };
        }
        public bool End => P >= b.Length;
    }

    public static PexFile Read(byte[] data)
    {
        var r = new R(data);
        var f = new PexFile();
        if (r.U32() != 0xFA57C0DE) throw new InvalidDataException("not a Skyrim pex (magic)");
        f.Major = r.U8(); f.Minor = r.U8(); f.GameId = r.U16(); f.CompileTime = r.U64();
        f.Source = r.WStr(); f.User = r.WStr(); f.Machine = r.WStr();
        int ns = r.U16();
        for (int i = 0; i < ns; i++) r.Str.Add(r.WStr());
        if (r.U8() != 0)
        {
            f.HadDebugInfo = true;
            r.U64();
            int nf = r.U16();
            for (int i = 0; i < nf; i++) { r.U16(); r.U16(); r.U16(); r.U8(); int n = r.U16(); r.P += 2 * n; }
        }
        int nuf = r.U16();
        for (int i = 0; i < nuf; i++) f.UserFlagDefs.Add((r.S(), r.U8()));
        int no = r.U16();
        for (int i = 0; i < no; i++)
        {
            var o = new PexObject { Name = r.S() };
            o.SizeOnDisk = r.U32();
            o.Parent = r.S(); o.Doc = r.S(); o.UserFlags = r.U32(); o.AutoState = r.S();
            int nv = r.U16();
            for (int j = 0; j < nv; j++)
                o.Variables.Add(new PexVariable { Name = r.S(), Type = r.S(), UserFlags = r.U32(), Value = r.Val() });
            int np = r.U16();
            for (int j = 0; j < np; j++)
            {
                var p = new PexProperty { Name = r.S(), Type = r.S(), Doc = r.S(), UserFlags = r.U32(), Flags = r.U8() };
                if ((p.Flags & 4) != 0) p.AutoVar = r.S();
                else
                {
                    if ((p.Flags & 1) != 0) p.Read = ReadFn(r);
                    if ((p.Flags & 2) != 0) p.Write = ReadFn(r);
                }
                o.Properties.Add(p);
            }
            int nst = r.U16();
            for (int j = 0; j < nst; j++)
            {
                var st = new PexState { Name = r.S() };
                int nfn = r.U16();
                for (int k = 0; k < nfn; k++) { var nm = r.S(); st.Functions.Add((nm, ReadFn(r))); }
                o.States.Add(st);
            }
            f.Objects.Add(o);
        }
        if (!r.End) throw new InvalidDataException($"trailing bytes: {data.Length - r.P}");
        return f;
    }

    static PexFunction ReadFn(R r)
    {
        var fn = new PexFunction { ReturnType = r.S(), Doc = r.S(), UserFlags = r.U32(), Flags = r.U8() };
        int np = r.U16(); for (int i = 0; i < np; i++) fn.Params.Add((r.S(), r.S()));
        int nl = r.U16(); for (int i = 0; i < nl; i++) fn.Locals.Add((r.S(), r.S()));
        int ni = r.U16();
        for (int i = 0; i < ni; i++)
        {
            byte op = r.U8();
            if (op >= FixedArgs.Length) throw new InvalidDataException($"bad opcode {op}");
            var ins = new Instr(op);
            for (int a = 0; a < FixedArgs[op]; a++) ins.Args.Add(r.Val());
            if (IsVariadic(op))
            {
                var cnt = r.Val();
                for (int a = 0; a < (int)cnt.Val!; a++) ins.VarArgs.Add(r.Val());
            }
            fn.Code.Add(ins);
        }
        return fn;
    }

    // ---------- writing ----------
    class W
    {
        public MemoryStream M = new();
        public Dictionary<string, ushort> Idx;
        public W(Dictionary<string, ushort> idx) { Idx = idx; }
        public void U8(byte v) => M.WriteByte(v);
        public void U16(int v) { M.WriteByte((byte)(v >> 8)); M.WriteByte((byte)v); }
        public void U32(uint v) { U16((int)(v >> 16)); U16((int)(v & 0xFFFF)); }
        public void U64(ulong v) { U32((uint)(v >> 32)); U32((uint)v); }
        public void WStr(string s) { var b = Encoding.UTF8.GetBytes(s); U16(b.Length); M.Write(b); }
        public void S(string s) => U16(Idx[s]);
        public void Val(V v)
        {
            U8((byte)v.T);
            switch (v.T)
            {
                case VT.Ident: case VT.Str: S((string)v.Val!); break;
                case VT.Int: U32((uint)(int)v.Val!); break;
                case VT.Float: U32((uint)BitConverter.SingleToInt32Bits((float)v.Val!)); break;
                case VT.Bool: U8((bool)v.Val! ? (byte)1 : (byte)0); break;
            }
        }
    }

    IEnumerable<string> AllStrings()
    {
        foreach (var u in UserFlagDefs) yield return u.Name;
        foreach (var o in Objects)
        {
            yield return o.Name; yield return o.Parent; yield return o.Doc; yield return o.AutoState;
            foreach (var v in o.Variables) { yield return v.Name; yield return v.Type; if (v.Value.Val is string s) yield return s; }
            foreach (var p in o.Properties)
            {
                yield return p.Name; yield return p.Type; yield return p.Doc;
                if ((p.Flags & 4) != 0) yield return p.AutoVar;
                foreach (var fn in new[] { p.Read, p.Write }) if (fn != null) foreach (var s in FnStrings(fn)) yield return s;
            }
            foreach (var st in o.States)
            {
                yield return st.Name;
                foreach (var (n, fn) in st.Functions) { yield return n; foreach (var s in FnStrings(fn)) yield return s; }
            }
        }
    }

    static IEnumerable<string> FnStrings(PexFunction fn)
    {
        yield return fn.ReturnType; yield return fn.Doc;
        foreach (var (a, b) in fn.Params) { yield return a; yield return b; }
        foreach (var (a, b) in fn.Locals) { yield return a; yield return b; }
        foreach (var ins in fn.Code)
            foreach (var v in ins.Args.Concat(ins.VarArgs))
                if (v.Val is string s) yield return s;
    }

    public byte[] Write()
    {
        var table = new List<string>();
        var idx = new Dictionary<string, ushort>();
        foreach (var s in AllStrings()) if (!idx.ContainsKey(s)) { idx[s] = (ushort)table.Count; table.Add(s); }

        var w = new W(idx);
        w.U32(0xFA57C0DE); w.U8(Major); w.U8(Minor); w.U16(GameId); w.U64(CompileTime);
        w.WStr(Source); w.WStr(User); w.WStr(Machine);
        w.U16(table.Count); foreach (var s in table) w.WStr(s);
        w.U8(0); // no debug info
        w.U16(UserFlagDefs.Count); foreach (var (n, i) in UserFlagDefs) { w.S(n); w.U8(i); }
        w.U16(Objects.Count);
        foreach (var o in Objects)
        {
            var ow = new W(idx);
            ow.S(o.Parent); ow.S(o.Doc); ow.U32(o.UserFlags); ow.S(o.AutoState);
            ow.U16(o.Variables.Count);
            foreach (var v in o.Variables) { ow.S(v.Name); ow.S(v.Type); ow.U32(v.UserFlags); ow.Val(v.Value); }
            ow.U16(o.Properties.Count);
            foreach (var p in o.Properties)
            {
                ow.S(p.Name); ow.S(p.Type); ow.S(p.Doc); ow.U32(p.UserFlags); ow.U8(p.Flags);
                if ((p.Flags & 4) != 0) ow.S(p.AutoVar);
                else { if (p.Read != null) WriteFn(ow, p.Read); if (p.Write != null) WriteFn(ow, p.Write); }
            }
            ow.U16(o.States.Count);
            foreach (var st in o.States)
            {
                ow.S(st.Name); ow.U16(st.Functions.Count);
                foreach (var (n, fn) in st.Functions) { ow.S(n); WriteFn(ow, fn); }
            }
            var body = ow.M.ToArray();
            w.S(o.Name);
            w.U32((uint)body.Length + 4); // size field counts itself
            w.M.Write(body);
        }
        return w.M.ToArray();
    }

    static void WriteFn(W w, PexFunction fn)
    {
        w.S(fn.ReturnType); w.S(fn.Doc); w.U32(fn.UserFlags); w.U8(fn.Flags);
        w.U16(fn.Params.Count); foreach (var (a, b) in fn.Params) { w.S(a); w.S(b); }
        w.U16(fn.Locals.Count); foreach (var (a, b) in fn.Locals) { w.S(a); w.S(b); }
        w.U16(fn.Code.Count);
        foreach (var ins in fn.Code)
        {
            if (ins.Args.Count != FixedArgs[ins.Op]) throw new InvalidOperationException($"{OpNames[ins.Op]} needs {FixedArgs[ins.Op]} args");
            w.U8(ins.Op);
            foreach (var a in ins.Args) w.Val(a);
            if (IsVariadic(ins.Op)) { w.Val(V.I(ins.VarArgs.Count)); foreach (var a in ins.VarArgs) w.Val(a); }
            else if (ins.VarArgs.Count > 0) throw new InvalidOperationException("varargs on non-call op");
        }
    }

    // ---------- disassembly ----------
    public string Disassemble()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"; pex {Major}.{Minor} game {GameId} source={Source} user={User} machine={Machine} debug={HadDebugInfo}");
        sb.AppendLine("; userflags: " + string.Join(", ", UserFlagDefs.Select(u => $"{u.Name}={u.Index}")));
        foreach (var o in Objects)
        {
            sb.AppendLine($".object {o.Name} extends {o.Parent} flags={o.UserFlags} autostate='{o.AutoState}' size={o.SizeOnDisk}");
            foreach (var v in o.Variables) sb.AppendLine($"  .var {v.Type} {v.Name} flags={v.UserFlags} = {v.Value}");
            foreach (var p in o.Properties)
            {
                sb.AppendLine($"  .property {p.Type} {p.Name} flags={p.Flags} uflags={p.UserFlags} auto={p.AutoVar}");
                if (p.Read != null) Dis(sb, "    get", p.Read);
                if (p.Write != null) Dis(sb, "    set", p.Write);
            }
            foreach (var st in o.States)
            {
                sb.AppendLine($"  .state '{st.Name}'");
                foreach (var (n, fn) in st.Functions) Dis(sb, "    function " + n, fn);
            }
        }
        return sb.ToString();
    }

    static void Dis(StringBuilder sb, string head, PexFunction fn)
    {
        sb.AppendLine($"{head}({string.Join(", ", fn.Params.Select(p => p.Type + " " + p.Name))}) -> {fn.ReturnType} flags={fn.Flags} uflags={fn.UserFlags}");
        foreach (var (n, t) in fn.Locals) sb.AppendLine($"      .local {t} {n}");
        for (int i = 0; i < fn.Code.Count; i++)
        {
            var ins = fn.Code[i];
            var args = string.Join(" ", ins.Args.Select(a => a.ToString()));
            if (ins.VarArgs.Count > 0 || ins.Op is 0x17 or 0x18 or 0x19) args += " [" + string.Join(" ", ins.VarArgs) + "]";
            sb.AppendLine($"      {i,3}: {OpNames[ins.Op]} {args}");
        }
    }
}
