using System.Diagnostics;
using System.Text.Json;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installers;
using CmlLib.Core.ProcessBuilder;

namespace GmuLauncher.Services;

public class LauncherService
{
    /// <summary>قائمة كل الإصدارات (Release / Snapshot / Beta / Alpha) من خوادم Mojang الرسمية.</summary>
    public async Task<List<VersionInfo>> GetVersionsAsync(CancellationToken ct = default)
    {
        var json = await Net.Http.GetStringAsync("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json", ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("versions").EnumerateArray().Select(v => new VersionInfo
        {
            Id = v.GetProperty("id").GetString() ?? "",
            Type = v.GetProperty("type").GetString() ?? "release",
            ReleaseTime = v.GetProperty("releaseTime").GetDateTime()
        }).ToList();
    }

    /// <summary>
    /// تثبيت ملف تعريف Fabric/Quilt: نحمّل الـ profile JSON من خوادمهم الرسمية ونحفظه في versions،
    /// والمكتبة CmlLib تتولى لاحقاً تنزيل المكتبات (inheritsFrom).
    /// </summary>
    public async Task<string> EnsureLoaderAsync(Instance inst, CancellationToken ct = default)
    {
        if (inst.Loader == "Vanilla") return inst.McVersion;
        if (inst.Loader is not ("Fabric" or "Quilt")) throw new NotSupportedException(Loc.T("Status_LoaderUnsupported"));

        var meta = inst.Loader == "Fabric" ? "https://meta.fabricmc.net/v2" : "https://meta.quiltmc.org/v3";
        var listJson = await Net.Http.GetStringAsync($"{meta}/versions/loader/{inst.McVersion}", ct);
        using var list = JsonDocument.Parse(listJson);
        if (list.RootElement.GetArrayLength() == 0) throw new InvalidOperationException($"{inst.Loader}: no loader for {inst.McVersion}");
        var loaderVer = list.RootElement[0].GetProperty("loader").GetProperty("version").GetString();

        var profile = await Net.Http.GetStringAsync($"{meta}/versions/loader/{inst.McVersion}/{loaderVer}/profile/json", ct);
        using var pdoc = JsonDocument.Parse(profile);
        var id = pdoc.RootElement.GetProperty("id").GetString()!;
        var dir = Path.Combine(Paths.Minecraft, "versions", id);
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, id + ".json"), profile, ct);
        return id;
    }

    /// <summary>المكتبات والـ assets والـ runtime مشتركة بين النسخ، ومجلد اللعب (gameDir) خاص بكل نسخة.</summary>
    MinecraftPath MakePath(Instance inst)
    {
        var root = Paths.Minecraft;
        var p = new MinecraftPath(inst.Folder);
        p.Library = Path.Combine(root, "libraries");
        p.Versions = Path.Combine(root, "versions");
        p.Assets = Path.Combine(root, "assets");
        p.Runtime = Path.Combine(root, "runtime");
        return p;
    }

    /// <summary>فحص + تنزيل ملفات اللعبة (Assets / Libraries / Natives) ثم بناء العملية (بدون تشغيلها).</summary>
    public async Task<Process> PrepareAsync(Instance inst, MSession session, IProgress<(string text, double pct)> progress, CancellationToken ct)
    {
        var st = AppState.I.Settings;
        Directory.CreateDirectory(inst.Folder);
        var launcher = new MinecraftLauncher(MakePath(inst));

        int ram = inst.RamMb > 0 ? inst.RamMb : st.RamMb;
        var opt = new MLaunchOption
        {
            Session = session,
            MaximumRamMb = ram,
            MinimumRamMb = Math.Min(1024, ram)
        };
        if (st.ResW > 0 && st.ResH > 0) { opt.ScreenWidth = st.ResW; opt.ScreenHeight = st.ResH; }
        if (!string.IsNullOrWhiteSpace(st.JavaPath)) opt.JavaPath = st.JavaPath;   // وإلا يُنزَّل Java المناسب تلقائياً
        if (!string.IsNullOrWhiteSpace(st.JvmArgs))
            opt.ExtraJvmArguments = st.JvmArgs.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(a => new MArgument(a)).ToArray();

        var fileProgress = new Progress<InstallerProgressChangedEventArgs>(e =>
        {
            double pct = e.TotalTasks > 0 ? e.ProgressedTasks * 100.0 / e.TotalTasks : 0;
            progress.Report(($"{e.Name} ({e.ProgressedTasks}/{e.TotalTasks})", pct));
        });

        null var proc = await launcher.InstallAndBuildProcessAsync(inst.LaunchVersion, opt, fileProgress: fileProgress, cancellationToken: ct);
        proc.StartInfo.UseShellExecute = false;
        proc.StartInfo.RedirectStandardOutput = true;
        proc.StartInfo.RedirectStandardError = true;
        proc.StartInfo.CreateNoWindow = true;
        return proc;
    }
}
