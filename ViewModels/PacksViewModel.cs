using Microsoft.Win32;

namespace GmuLauncher.ViewModels;

public partial class PacksViewModel : ObservableObject
{
    public AppState State => AppState.I;
    public ObservableCollection<PackItem> Packs { get; } = new();

    public PacksViewModel()
    {
        State.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(AppState.SelectedInstance)) Reload(); };
        Reload();
    }

    void Reload()
    {
        Packs.Clear();
        var inst = State.SelectedInstance;
        if (inst == null) return;
        foreach (var p in PackReader.Load(inst))
        {
            p.Toggled += x => PackReader.SetEnabled(inst.Folder, x.Name, x.IsEnabled);
            Packs.Add(p);
        }
    }

    [RelayCommand]
    private void Add()
    {
        var inst = State.SelectedInstance;
        if (inst == null) return;
        var dlg = new OpenFileDialog { Filter = "Resource Pack (*.zip)|*.zip", Multiselect = true };
        if (dlg.ShowDialog() != true) return;
        Directory.CreateDirectory(inst.PacksFolder);
        foreach (var f in dlg.FileNames) File.Copy(f, Path.Combine(inst.PacksFolder, Path.GetFileName(f)), true);
        Reload();
    }
}
