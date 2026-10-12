namespace GmuLauncher.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public AppState State => AppState.I;
    readonly Dictionary<string, ObservableObject> _pages;

    [ObservableProperty] private object? _currentPage;
    [ObservableProperty] private bool _isCompact;

    public MainViewModel()
    {
        _pages = new()
        {
            ["Home"] = new HomeViewModel(),
            ["Instances"] = new InstancesViewModel(),
            ["Mods"] = new ModsViewModel(),
            ["Packs"] = new PacksViewModel(),
            ["Skins"] = new SkinsViewModel(),
            ["Settings"] = new SettingsViewModel(),
        };
        CurrentPage = _pages["Home"];
    }

    [RelayCommand]
    private void Navigate(string? page)
    {
        if (page != null && _pages.TryGetValue(page, out var p)) CurrentPage = p;
    }

    [RelayCommand] private void SwitchAccount() => App.SwitchAccount();
    [RelayCommand] private async Task LogoutAsync() => await App.LogoutAsync();
}
