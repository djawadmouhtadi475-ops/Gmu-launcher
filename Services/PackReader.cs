using System.IO.Compression;
using System.Text.Json;
using System.Windows.Media.Imaging;

namespace GmuLauncher.Services;

/// <summary>قراءة الـ Resource Packs وحالة تفعيلها من ملف options.txt الخاص بالنسخة.</summary>
public static class PackReader
{
    public static List<PackItem> Load(Instance inst)
    {
        var list = new List<PackItem>();
        if (!Directory.Exists(inst.PacksFolder)) return list;
        var enabled = ReadEnabled(inst.Folder);
        var entries = Directory.GetFiles(inst.PacksFolder, "*.zip").Concat(Directory.GetDirectories(inst.PacksFolder));
        foreach (var p in entries)
        {
            var name = Path.GetFileName(p);
            var item = new PackItem { Name = name, Path = p, Thumb = Thumb(p) };
            item.InitEnabled(enabled.Contains("file/" + name));
            list.Add(item);
        }
        return list;
    }

    static BitmapImage? Thumb(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                var f = System.IO.Path.Combine(path, "pack.png");
                if (!File.Exists(f)) return null;
                var b = new BitmapImage();
                b.BeginInit(); b.UriSource = new Uri(f); b.CacheOption = BitmapCacheOption.OnLoad; b.DecodePixelWidth = 96; b.EndInit(); b.Freeze();
                return b;
            }
            using var zip = ZipFile.OpenRead(path);
            var e = zip.GetEntry("pack.png");
            return e == null ? null : ModReader.LoadImage(e);
        }
        catch { return null; }
    }

    static HashSet<string> ReadEnabled(string gameDir)
    {
        var f = System.IO.Path.Combine(gameDir, "options.txt");
        if (!File.Exists(f)) return new();
        var line = File.ReadAllLines(f).FirstOrDefault(l => l.StartsWith("resourcePacks:"));
        if (line == null) return new();
        try { return JsonSerializer.Deserialize<List<string>>(line["resourcePacks:".Length..])?.ToHashSet() ?? new(); }
        catch { return new(); }
    }

    public static void SetEnabled(string gameDir, string packName, bool on)
    {
        var f = System.IO.Path.Combine(gameDir, "options.txt");
        var lines = File.Exists(f) ? File.ReadAllLines(f).ToList() : new List<string>();
        int idx = lines.FindIndex(l => l.StartsWith("resourcePacks:"));
        var list = new List<string> { "vanilla" };
        if (idx >= 0)
            try { list = JsonSerializer.Deserialize<List<string>>(lines[idx]["resourcePacks:".Length..]) ?? list; } catch { }
        var entry = "file/" + packName;
        list.RemoveAll(x => x == entry);
        if (on) list.Add(entry);
        var newLine = "resourcePacks:" + JsonSerializer.Serialize(list);
        if (idx >= 0) lines[idx] = newLine; else lines.Add(newLine);
        Directory.CreateDirectory(gameDir);
        File.WriteAllLines(f, lines);
    }
}
