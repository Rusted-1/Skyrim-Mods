using System.IO.Compression;
using MsgTool;

switch (args[0])
{
    case "dis":   // dis <file.pex>
        Console.Write(PexFile.Read(File.ReadAllBytes(args[1])).Disassemble());
        break;
    case "roundtrip": // roundtrip <dir>: parse every pex, re-write it, re-parse and compare disassembly
        foreach (var f in Directory.GetFiles(args[1], "*.pex"))
        {
            var p = PexFile.Read(File.ReadAllBytes(f));
            var again = PexFile.Read(p.Write());
            var a = p.Disassemble().Split('\n').Skip(2).Select(l => System.Text.RegularExpressions.Regex.Replace(l, @" size=\d+", ""));
            var b = again.Disassemble().Split('\n').Skip(2).Select(l => System.Text.RegularExpressions.Regex.Replace(l, @" size=\d+", ""));
            bool sizeOk = p.Objects.Zip(again.Objects).All(z => z.First.SizeOnDisk - z.Second.SizeOnDisk == 0 || p.HadDebugInfo);
            Console.WriteLine($"{(a.SequenceEqual(b) ? "OK " : "DIFF")} size={(sizeOk ? "same" : $"{p.Objects[0].SizeOnDisk}/{again.Objects[0].SizeOnDisk}")} {Path.GetFileName(f)}");
        }
        break;
    case "esp":   // esp <file.esp> [TYPE]: list records, dump subrecords of TYPE
        {
            var e = Esp.Read(File.ReadAllBytes(args[1]));
            Console.WriteLine($"TES4 flags={e.Header.Flags:X} ver={e.Header.Version}");
            foreach (var s in e.Header.Subs) Console.WriteLine($"  {s.Type} [{s.Data.Length}] {(s.Type is "MAST" or "CNAM" or "SNAM" ? s.ZStr : Esp.Hex(s.Data, 64))}");
            foreach (var r in e.AllRecords())
            {
                if (args.Length > 2 && r.Type != args[2] && r.Edid != args[2]) continue;
                Console.WriteLine($"{r.Type} {r.FormId:X8} flags={r.Flags:X} ver={r.Version} {r.Edid}");
                if (args.Length > 2)
                    foreach (var s in r.Subs) Console.WriteLine($"  {s.Type} [{s.Data.Length}] {Esp.Hex(s.Data, 100000)}");
            }
        }
        break;
    case "zip":   // zip <folder> <out.zip>: zips the folder's contents (forward-slash entry names for mod managers)
        {
            File.Delete(args[2]);
            using var z = System.IO.Compression.ZipFile.Open(args[2], System.IO.Compression.ZipArchiveMode.Create);
            foreach (var f in Directory.GetFiles(args[1], "*", SearchOption.AllDirectories))
                z.CreateEntryFromFile(f, Path.GetRelativePath(args[1], f).Replace('\\', '/'));
            Console.WriteLine("zipped " + args[2]);
        }
        break;
    default:
        Build.Run(args);
        break;
}
