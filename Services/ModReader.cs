using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;

namespace GmuLauncher.Services;

/// <summary>قراءة بيانات المود من داخل ملف .jar (Fabric / Quilt / Forge / NeoForge).</summary>
public static class ModReader
{
    public static LocalMod Read(string path)
    {
        var m = new LocalMod { FilePath = path, FileName = Path.GetFileName(path) };
        var plain = m.FileName.EndsWith(".disabled") ? m.FileName[..^".disabled".Length] : m.FileName;
        m.Name = Path.GetFileNameWithoutExtension(plain);
        m.InitEnabled(!m.FileName.EndsWith(".disabled"));
        string? logoPath = null;

        try
        {
            using var zip = ZipFile.OpenRead(path);
            var fabric = zip.GetEntry("fabric.mod.json");
            var quilt = zip.GetEntry("quilt.mod.json");
            var toml = zip.GetEntry("META-INF/neoforge.mods.toml") ?? zip.GetEntry("META-INF/mods.toml");

            if (fabric != null)
            {
                m.Loader = "Fabric";
                using var d = ReadJson(fabric);
                var r = d.RootElement;
                m.Name = Str(r, "name") ?? m.Name;
                m.Version = Str(r, "version") ?? "";
                m.Description = Str(r, "description") ?? "";
                if (r.TryGetProperty("icon", out var ic))
                {
                    if (ic.ValueKind == JsonValueKind.String) logoPath = ic.GetString();
                    else if (ic.ValueKind == JsonValueKind.Object)
                        logoPath = ic.EnumerateObject().OrderByDescending(p => int.TryParse(p.Name, out var n) ? n : 0).Select(p => p.Value.GetString()).FirstOrDefault();
                }
                if (r.TryGetProperty("depends", out var dep) && dep.ValueKind == JsonValueKind.Object && dep.TryGetProperty("minecraft", out var mc))
                    m.McRange = mc.ValueKind == JsonValueKind.Array ? string.Join("||", mc.EnumerateArray().Select(x => x.GetString())) : mc.GetString();
            }
            else if (quilt != null)
            {
                m.Loader = "Quilt";
                using var d = ReadJson(quilt);
                if (d.RootElement.TryGetProperty("quilt_loader", out var ql))
                {
                    m.Version = Str(ql, "version") ?? "";
                    if (ql.TryGetProperty("metadata", out var md))
                    {
                        m.Name = Str(md, "name") ?? m.Name;
                        m.Description = Str(md, "description") ?? "";
                        logoPath = Str(md, "icon");
                    }
                    if (ql.TryGetProperty("depends", out var deps) && deps.ValueKind == JsonValueKind.Array)
                        foreach (var x in deps.EnumerateArray())
                            if (x.ValueKind == JsonValueKind.Object && Str(x, "id") == "minecraft" && x.TryGetProperty("versions", out var v))
                                m.McRange = v.ValueKind == JsonValueKind.Array ? string.Join("||", v.EnumerateArray().Select(y => y.GetString())) : v.GetString();
                }
            }
            else if (toml != null)
            {
                m.Loader = toml.FullName.Contains("neoforge") ? "NeoForge" : "Forge";
                using var sr = new StreamReader(toml.Open());
                var t = sr.ReadToEnd();
                string? G(string pattern)
                {
                    var mm = Regex.Match(t, pattern);
                    return mm.Success && mm.Groups[1].Value.Length > 0 ? mm.Groups[1].Value.Trim() : null;
                }
                m.Name = G(@"displayName\s*=\s*""([^""]+)""") ?? m.Name;
                var ver = G(@"(?m)^\s*version\s*=\s*""([^""]+)""");
                if (ver != null && !ver.StartsWith("${")) m.Version = ver;
                m.Description = G(@"description\s*=\s*'''([\s\S]*?)'''") ?? G(@"description\s*=\s*""([^""]+)""") ?? "";
                logoPath = G(@"logoFile\s*=\s*""([^""]+)""");
                m.McRange = G(@"modId\s*=\s*""minecraft""[\s\S]*?versionRange\s*=\s*""([^""]+)""");
            }

            if (!string.IsNullOrEmpty(logoPath))
            {
                var e = zip.GetEntry(logoPath.TrimStart('/'));
                if (e != null) m.Logo = LoadImage(e);
            }
        }
        catch { /* ملف تالف أو بتنسيق غير معروف: نكتفي باسم الملف */ }
        return m;
    }

    static JsonDocument ReadJson(ZipArchiveEntry e)
    {
        using var s = e.Open();
        return JsonDocument.Parse(s, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
    }

    static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    public static BitmapImage? LoadImage(ZipArchiveEntry e)
    {
        try
        {
            var ms = new MemoryStream();
            using (var s = e.Open()) s.CopyTo(ms);
            ms.Position = 0;
            var b = new BitmapImage();
            b.BeginInit();
            b.StreamSource = ms;
            b.CacheOption = BitmapCacheOption.OnLoad;
            b.DecodePixelWidth = 64;
            b.EndInit();
            b.Freeze();   // مهم: لاستخدامه من خيط الواجهة
            return b;
        }
        catch { return null; }
    }

    // ---------------- فحص التوافق مع إصدار Minecraft ----------------
    // هذا فحص تقريبي (heuristic): يدعم صيغ Fabric (>=1.20 <1.21 / 1.20.x / ~1.20)
    // وصيغ Maven المستخدمة في Forge ([1.20,1.21)).
    public static bool Compatible(string? range, string mc)
    {
        if (string.IsNullOrWhiteSpace(range) || range.Trim() == "*") return true;
        if (!Version.TryParse(Norm(mc), out var v)) return true;   // snapshots وما شابه
        foreach (var alt in range.Split("||")) if (AltOk(alt.Trim(), v, mc)) return true;
        return false;
    }

    static string Norm(string s) => s.Split('.').Length == 2 ? s + ".0" : s;

    static bool EqPrefix(string num, string mc, int max = int.MaxValue)
    {
        var a = num.Split('.'); var b = mc.Split('.');
        for (int i = 0; i < a.Length && i < b.Length && i < max; i++)
            if (a[i] != b[i] && a[i] != "x" && a[i] != "*") return false;
        return true;
    }

    static bool AltOk(string alt, Version v, string mc)
    {
        if (alt.Length >= 2 && (alt[0] == '[' || alt[0] == '('))
        {
            bool lowInc = alt[0] == '[', upInc = alt[^1] == ']';
            var inner = alt.Substring(1, alt.Length - 2).Split(',');
            if (inner.Length == 1) return EqPrefix(inner[0].Trim(), mc);
            bool ok = true;
            if (Version.TryParse(Norm(inner[0].Trim()), out var lo)) ok &= lowInc ? v >= lo : v > lo;
            if (Version.TryParse(Norm(inner[1].Trim()), out var hi)) ok &= upInc ? v <= hi : v < hi;
            return ok;
        }
        foreach (var t in alt.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var m = Regex.Match(t, @"^(>=|<=|>|<|=|~|\^)?(\d+(?:\.\d+)*)");
            if (!m.Success) continue;
            var op = m.Groups[1].Value; var num = m.Groups[2].Value;
            if (!Version.TryParse(Norm(num), out var n)) { if (!EqPrefix(num, mc)) return false; continue; }
            bool r = op switch
            {
                ">=" => v >= n, "<=" => v <= n, ">" => v > n, "<" => v < n,
                "~" or "^" => EqPrefix(num, mc, 2),
                _ => EqPrefix(num, mc)
            };
            if (!r) return false;
        }
        return true;
    }
}
