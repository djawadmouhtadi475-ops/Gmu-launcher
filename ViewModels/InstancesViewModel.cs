using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Data;

namespace GmuLauncher.ViewModels;

public partial class InstancesViewModel : ObservableObject
{
    public AppState State => AppState.I;
    readonly List<VersionInfo> _all = new();

    public ObservableCollection<VersionInfo> Versions { get; } = new();
    public ICollectionView InstancesView { get; }
    public string[] VersionTypes { get; } = { "release", "snapshot", "old_beta", "old_alpha" };
    public string[] FilterTypes { get; } = { "All", "release", "snapshot", "old_beta", "old_alpha" };
    public string[] Loaders { get; } = { "Vanilla", "Fabric", "Quilt", "Forge", "NeoForge" };

    [ObservableProperty] private string _newName = "";
    [ObservableProperty] private string _selectedType = "release";
    [ObservableProperty] private string _selectedLoader = "Vanilla";
    [ObservableProperty] private VersionInfo? _selectedVersion;
    [ObservableProperty] private string _search = "";
    [ObservableProperty] private string _filterType = "All";
    [ObservableProperty] private string _message = "";

    public InstancesViewModel()
    {
        InstancesView = new CollectionViewSource { Source = State.Instances }.View;
        InstancesView.Filter = o => o is Instance i
            && (FilterType == "All" || i.VersionType == FilterType)
            && (string.IsNullOrWhiteSpace(Search)
                || i.Name.Contains(Search, StringComparison.OrdinalIgnoreCase)
                || i.McVersion.Contains(Search, StringComparison.OrdinalIgnoreCase));
        _ = LoadVersionsAsync();
    }

    partial void OnSelectedTypeChanged(string value) => ApplyVersionFilter();
    partial void OnSearchChanged(string value) => InstancesView.Refresh();
    partial void OnFilterTypeChanged(string value) => InstancesView.Refresh();

    async Task LoadVersionsAsync()
    {
        try
        {
            _all.Clear();
            _all.AddRange(await State.Launcher.GetVersionsAsync());
            ApplyVersionFilter();
        }
        catch (Exception ex) { Message = ex.Message; }
    }

    void ApplyVersionFilter()
    {
        Versions.Clear();
        foreach (var v in _all.Where(v => v.Type == SelectedType)) Versions.Add(v);
        SelectedVersion = Versions.FirstOrDefault();
    }

    [RelayCommand]
    private void Create()
    {
        Message = "";
        var name = NewName.Trim();
        if (name == "" || SelectedVersion == null) { Message = Loc.T("Inst_Invalid"); return; }
        var folder = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c));
        if (State.Instances.Any(i => i.FolderName.Equals(folder, StringComparison.OrdinalIgnoreCase))) { Message = Loc.T("Inst_Exists"); return; }

        var inst = new Instance
        {
            Name = name, FolderName = folder, McVersion = SelectedVersion.Id, VersionType = SelectedVersion.Type,
            Loader = SelectedLoader, LaunchVersion = SelectedVersion.Id
        };
        Directory.CreateDirectory(inst.Folder);
        Directory.CreateDirectory(inst.ModsFolder);
        State.Db.SaveInstance(inst);
        State.Instances.Add(inst);
        State.SelectedInstance = inst;
        NewName = "";
    }

    [RelayCommand]
    private void OpenFolder(Instance? i)
    {
        if (i == null) return;
        Directory.CreateDirectory(i.Folder);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{i.Folder}\"") { UseShellExecute = true });
    }

    [RelayCommand]
    private void Delete(Instance? i)
    {
        if (i == null) return;
        if (MessageBox.Show(Loc.T("Inst_DeleteConfirm"), i.Name, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try { if (Directory.Exists(i.Folder)) Directory.Delete(i.Folder, true); } catch (Exception ex) { Message = ex.Message; }
        State.Db.DeleteInstance(i.Id);
        State.Instances.Remove(i);
        if (State.SelectedInstance == i) State.SelectedInstance = State.Instances.FirstOrDefault();
    }
}
