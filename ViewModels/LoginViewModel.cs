using System.Text.RegularExpressions;

namespace GmuLauncher.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    public event Action? LoggedIn;
    AppState St => AppState.I;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsOfflineMode))] private bool _isMicrosoftMode = true;
    public bool IsOfflineMode => !IsMicrosoftMode;
    [ObservableProperty] private string _username = "";
    [ObservableProperty] private bool _remember = true;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _error = "";
    public ObservableCollection<Account> Saved { get; } = new();
    public bool HasSaved => Saved.Count > 0;

    public LoginViewModel()
    {
        foreach (var a in St.Db.GetAccounts()) Saved.Add(a);
        Saved.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasSaved));
    }

    [RelayCommand] private void SetMode(string mode) => IsMicrosoftMode = mode == "ms";

    /// <summary>يفتح نافذة المصادقة الرسمية: Microsoft → Xbox Live → XSTS → Minecraft token.</summary>
    [RelayCommand]
    private async Task MicrosoftLoginAsync()
    {
        Error = ""; IsBusy = true;
        try
        {
            var s = await St.Auth.MicrosoftInteractiveAsync();
            St.SetSession(s, "Microsoft", Remember);
            LoggedIn?.Invoke();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void OfflineLogin()
    {
        Error = "";
        var name = Username.Trim();
        if (!Regex.IsMatch(name, "^[A-Za-z0-9_]{3,16}$")) { Error = Loc.T("Login_Invalid"); return; }
        St.SetSession(AuthService.Offline(name), "Offline", Remember);
        LoggedIn?.Invoke();
    }

    [RelayCommand]
    private async Task UseSavedAsync(Account? acc)
    {
        if (acc == null) return;
        if (acc.Type == "Offline")
        {
            St.SetSession(AuthService.Offline(acc.Username), "Offline", Remember);
            LoggedIn?.Invoke();
            return;
        }
        Error = ""; IsBusy = true;
        try
        {
            // نحاول الدخول الصامت بالـ Refresh Token، وإن فشل نفتح المصادقة الرسمية
            CmlLib.Core.Auth.MSession s;
            try { s = await St.Auth.MicrosoftSilentAsync(); }
            catch { s = await St.Auth.MicrosoftInteractiveAsync(); }
            St.SetSession(s, "Microsoft", Remember);
            LoggedIn?.Invoke();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void RemoveSaved(Account? acc)
    {
        if (acc == null) return;
        St.Db.DeleteAccount(acc.Id);
        Saved.Remove(acc);
    }
}
