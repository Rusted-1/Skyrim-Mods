// Minimal Skyrim SE plugin (.esp) reader and writer. Little-endian.
using System.IO.Compression;
using System.Text;

namespace MsgTool;

public class Sub
{
    public string Type;
    public byte[] Data;
    public Sub(string t, byte[] d) { Type = t; Data = d; }
    public string ZStr => Encoding.UTF8.GetString(Data).TrimEnd('\0');
}

public class Rec
{
    public string Type = "";
    public uint Flags, FormId, Vcs1;
    public ushort Version = 44, Vcs2;
    public List<Sub> Subs = new();
    public Sub? Get(string t) => Subs.FirstOrDefault(s => s.Type == t);
    public string Edid => Get("EDID")?.ZStr ?? "";
}

public class Grp
{
    public string Label = "";
    public int GroupType;
    public uint LabelRaw, Stamp, Unk;
    public List<object> Items = new(); // Rec or Grp
}

public class Esp
{
    public Rec Header = new();
    public List<Grp> Groups = new();

    public IEnumerable<Rec> AllRecords() => Groups.SelectMany(Walk);
    static IEnumerable<Rec> Walk(Grp g)
    {
        foreach (var it in g.Items)
            if (it is Rec r) yield return r;
            else foreach (var x in Walk((Grp)it)) yield return x;
    }

    public static Esp Read(byte[] b)
    {
        var e = new Esp();
        int p = 0;
        e.Header = ReadRec(b, ref p);
        while (p < b.Length) e.Groups.Add(ReadGrp(b, ref p));
        return e;
    }

    static string Tag(byte[] b, int p) => Encoding.ASCII.GetString(b, p, 4);

    static Grp ReadGrp(byte[] b, ref int p)
    {
        if (Tag(b, p) != "GRUP") throw new InvalidDataException($"expected GRUP at {p}");
        int size = BitConverter.ToInt32(b, p + 4);
        var g = new Grp
        {
            LabelRaw = BitConverter.ToUInt32(b, p + 8),
            Label = Tag(b, p + 8),
            GroupType = BitConverter.ToInt32(b, p + 12),
            Stamp = BitConverter.ToUInt32(b, p + 16),
            Unk = BitConverter.ToUInt32(b, p + 20)
        };
        int end = p + size; p += 24;
        while (p < end)
            if (Tag(b, p) == "GRUP") g.Items.Add(ReadGrp(b, ref p));
            else g.Items.Add(ReadRec(b, ref p));
        return g;
    }

    static Rec ReadRec(byte[] b, ref int p)
    {
        var r = new Rec
        {
            Type = Tag(b, p),
            Flags = BitConverter.ToUInt32(b, p + 8),
            FormId = BitConverter.ToUInt32(b, p + 12),
            Vcs1 = BitConverter.ToUInt32(b, p + 16),
            Version = BitConverter.ToUInt16(b, p + 20),
            Vcs2 = BitConverter.ToUInt16(b, p + 22)
        };
        int size = BitConverter.ToInt32(b, p + 4);
        var data = b.AsSpan(p + 24, size).ToArray();
        p += 24 + size;
        if ((r.Flags & 0x40000) != 0)
        {
            using var z = new ZLibStream(new MemoryStream(data, 4, data.Length - 4), CompressionMode.Decompress);
            var ms = new MemoryStream(); z.CopyTo(ms); data = ms.ToArray();
            r.Flags &= ~0x40000u;
        }
        int q = 0; int big = -1;
        while (q < data.Length)
        {
            var t = Tag(data, q); int n = BitConverter.ToUInt16(data, q + 4); q += 6;
            if (t == "XXXX") { big = BitConverter.ToInt32(data, q); q += n; continue; }
            if (big >= 0) { n = big; big = -1; }
            r.Subs.Add(new Sub(t, data.AsSpan(q, n).ToArray())); q += n;
        }
        return r;
    }

    public byte[] Write()
    {
        var ms = new MemoryStream();
        WriteRec(ms, Header);
        foreach (var g in Groups) WriteGrp(ms, g);
        return ms.ToArray();
    }

    static void WriteGrp(Stream s, Grp g)
    {
        var body = new MemoryStream();
        foreach (var it in g.Items) if (it is Rec r) WriteRec(body, r); else WriteGrp(body, (Grp)it);
        var w = new BinaryWriter(s);
        w.Write(Encoding.ASCII.GetBytes("GRUP")); w.Write((int)body.Length + 24);
        w.Write(g.LabelRaw); w.Write(g.GroupType); w.Write(g.Stamp); w.Write(g.Unk);
        body.Position = 0; body.CopyTo(s);
    }

    static void WriteRec(Stream s, Rec r)
    {
        var body = new MemoryStream(); var bw = new BinaryWriter(body);
        foreach (var sub in r.Subs)
        {
            if (sub.Data.Length > 0xFFFF)
            {
                bw.Write(Encoding.ASCII.GetBytes("XXXX")); bw.Write((ushort)4); bw.Write(sub.Data.Length);
                bw.Write(Encoding.ASCII.GetBytes(sub.Type)); bw.Write((ushort)0);
            }
            else { bw.Write(Encoding.ASCII.GetBytes(sub.Type)); bw.Write((ushort)sub.Data.Length); }
            bw.Write(sub.Data);
        }
        var w = new BinaryWriter(s);
        w.Write(Encoding.ASCII.GetBytes(r.Type)); w.Write((int)body.Length);
        w.Write(r.Flags); w.Write(r.FormId); w.Write(r.Vcs1); w.Write(r.Version); w.Write(r.Vcs2);
        body.Position = 0; body.CopyTo(s);
    }

    // ---------- helpers ----------
    public static string Hex(byte[] d, int max = 4096) =>
        string.Join(" ", d.Take(max).Select(x => x.ToString("X2"))) + (d.Length > max ? " ..." : "");
    public static byte[] ZStr(string s) => Encoding.UTF8.GetBytes(s + "\0");
}
