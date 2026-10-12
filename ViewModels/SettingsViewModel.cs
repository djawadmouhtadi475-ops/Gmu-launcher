using Microsoft.Win32;

namespace GmuLauncher.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    AppState St => AppState.I;
    public LangItem[] Languages { get; } = { new("ar", "العربية"), new("en", "English") };

    [ObservableProperty] private double _ramGb;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(JavaManual))] private bool _javaAuto;
    public bool JavaManual => !JavaAuto;
    [ObservableProperty] private string _javaPath = "";
    [ObservableProperty] private string _jvmArgs = "";
    [ObservableProperty] private int _resW;
    [ObservableProperty] private int _resH;
    [ObservableProperty] private string _curseKey = "";
    [ObservableProperty] private string _lang = "ar";
    [ObservableProperty] private string _status = "";

    public SettingsViewModel()
    {
        var s = St.Settings;
        RamGb = Math.Clamp(s.RamMb / 1024.0, 1, 32);
        JavaPath = s.JavaPath; JavaAuto = string.IsNullOrWhiteSpace(s.JavaPath);
        JvmArgs = s.JvmArgs; ResW = s.ResW; ResH = s.ResH; CurseKey = s.CurseForgeKey; Lang = s.Lang;
    }

    [RelayCommand]
    private void BrowseJava()
    {
        var dlg = new OpenFileDialog { Filter = "javaw.exe|javaw.exe;java.exe" };
        if (dlg.ShowDialog() == true) { JavaPath = dlg.FileName; JavaAuto = false; }
    }

    [RelayCommand]
    private void Save()
    {
        var s = St.Settings;
        s.RamMb = (int)(RamGb * 1024);
        s.JavaPath = JavaAuto ? "" : JavaPath.Trim();
        s.JvmArgs = JvmArgs.Trim();
        s.ResW = ResW; s.ResH = ResH;
        s.CurseForgeKey = CurseKey.Trim();
        if (s.Lang != Lang) { s.Lang = Lang; Loc.Apply(Lang); }
        Status = Loc.T("Set_Saved");
    }

    [RelayCommand] private void ShowAbout() => new Views.AboutWindow { Owner = System.Windows.Application.Current.MainWindow }.ShowDialog();
}
