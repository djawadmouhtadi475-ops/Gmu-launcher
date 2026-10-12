using System.Windows;

namespace GmuLauncher.ViewModels;

public partial class ModsViewModel : ObservableObject
{
    public AppState State => AppState.I;
    readonly ModrinthService _modrinth = new();
    readonly CurseForgeService _curse = new();
    CancellationTokenSource? _dlCts;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsLocal))] private bool _isBrowse;
    public bool IsLocal => !IsBrowse;

    public string[] Sources { get; } = { "Modrinth", "CurseForge" };
    public string[] LoaderFilters { get; } = { "Fabric", "Forge", "Quilt", "NeoForge" };
    [ObservableProperty] private string _source = "Modrinth";
    [ObservableProperty] private string _query = "";
    [ObservableProperty] private string _loaderFilter = "Fabric";
    [ObservableProperty] private string _mcFilter = "";
    [ObservableProperty] private string _message = "";
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(DownloadCommand))] private bool _isDownloading;
    [ObservableProperty] private double _downloadProgress;
    [ObservableProperty] private string _downloadName = "";

    public ObservableCollection<ModResult> Results { get; } = new();
    public ObservableCollection<LocalMod> LocalMods { get; } = new();

    public ModsViewModel()
    {
        State.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.SelectedInstance)) OnInstanceChanged();
        };
        OnInstanceChanged();
    }

    void OnInstanceChanged()
    {
        var i = State.SelectedInstance;
        if (i != null)
        {
            McFilter = i.McVersion;
            if (LoaderFilters.Contains(i.Loader)) LoaderFilter = i.Loader;
        }
        _ = ReloadLocalAsync();
    }

    [RelayCommand] private void SetTab(string tab) => IsBrowse = tab == "browse";

    // ---------------- المودات المحلية ----------------
    public async Task ReloadLocalAsync()
    {
        var inst = State.SelectedInstance;
        LocalMods.Clear();
        if (inst == null || !Directory.Exists(inst.ModsFolder)) return;

        var files = Directory.GetFiles(inst.ModsFolder).Where(f => f.EndsWith(".jar") || f.EndsWith(".jar.disabled")).ToArray();
        var mods = await Task.Run(() => files.Select(ModReader.Read).OrderBy(m => m.Name).ToList());
        foreach (var m in mods)
        {
            // تحذير تلقائي عند عدم توافق إصدار المود مع إصدار النسخة
            if (!ModReader.Compatible(m.McRange, inst.McVersion))
                m.Warning = Loc.T("Mods_Mismatch") + (m.McRange != null ? $" ({m.McRange})" : "");
            m.Toggled += OnToggled;
            LocalMods.Add(m);
        }
    }

    /// <summary>تفعيل/تعطيل المود بإعادة تسمية الملف (.jar ⇄ .jar.disabled).</summary>
    void OnToggled(LocalMod m)
    {
        try
        {
            var newPath = m.IsEnabled ? m.FilePath[..^".disabled".Length] : m.FilePath + ".disabled";
            File.Move(m.FilePath, newPath);
            m.FilePath = newPath;
            m.FileName = Path.GetFileName(newPath);
            if (State.SelectedInstance != null)
                State.Db.SetModState(State.SelectedInstance.Id, m.FileName.Replace(".disabled", ""), m.IsEnabled);
        }
        catch (Exception ex) { Message = ex.Message; }
    }

    /// <summary>يُستدعى من حدث Drag &amp; Drop في الواجهة.</summary>
    public async Task AddFilesAsync(IEnumerable<string> paths)
    {
        var inst = State.SelectedInstance;
        if (inst == null) { Message = Loc.T("Mods_NoInstance"); return; }
        Directory.CreateDirectory(inst.ModsFolder);
        foreach (var p in paths.Where(p => p.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)))
            File.Copy(p, Path.Combine(inst.ModsFolder, Path.GetFileName(p)), true);
        await ReloadLocalAsync();
    }

    [RelayCommand]
    private void DeleteMod(LocalMod? m)
    {
        if (m == null) return;
        if (MessageBox.Show(Loc.T("Mods_DeleteConfirm") + "\n" + m.Name, "Gmu Launcher", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try { File.Delete(m.FilePath); LocalMods.Remove(m); } catch (Exception ex) { Message = ex.Message; }
    }

    // ---------------- التصفح والتنزيل ----------------
    IModSource CurrentSource => Source == "CurseForge" ? _curse : _modrinth;

    [RelayCommand]
    private async Task SearchAsync()
    {
        Message = ""; IsSearching = true;
        try
        {
            var list = await CurrentSource.SearchAsync(Query, McFilter, LoaderFilter, CancellationToken.None);
            Results.Clear();
            foreach (var r in list) Results.Add(r);
        }
        catch (Exception ex) { Message = ex.Message; }
        finally { IsSearching = false; }
    }

    [RelayCommand(CanExecute = nameof(CanDownload))]
    private async Task DownloadAsync(ModResult? r)
    {
        var inst = State.SelectedInstance;
        if (r == null) return;
        if (inst == null) { Message = Loc.T("Mods_NoInstance"); return; }

        _dlCts = new CancellationTokenSource();
        IsDownloading = true; DownloadProgress = 0; DownloadName = r.Title; Message = "";
        string dest = "";
        try
        {
            var (url, file) = await CurrentSource.GetDownloadAsync(r.Id, McFilter, LoaderFilter, _dlCts.Token);
            Directory.CreateDirectory(inst.ModsFolder);
            dest = Path.Combine(inst.ModsFolder, file);
            var prog = new Progress<double>(p => DownloadProgress = p);
            await Net.DownloadAsync(url, dest, prog, _dlCts.Token);
            State.Db.AddDownload(r.Title, r.Source, url, dest, "ok");
            Message = Loc.T("Mods_Done");
            await ReloadLocalAsync();
        }
        catch (OperationCanceledException) { Message = Loc.T("Status_Cancelled"); }
        catch (Exception ex)
        {
            Message = ex.Message;
            if (dest != "") State.Db.AddDownload(r.Title, r.Source, "", dest, "failed");
        }
        finally { IsDownloading = false; }
    }
    bool CanDownload(ModResult? r) => !IsDownloading;

    [RelayCommand] private void CancelDownload() => _dlCts?.Cancel();
}
