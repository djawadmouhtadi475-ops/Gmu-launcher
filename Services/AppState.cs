using CmlLib.Core.Auth;

namespace GmuLauncher.Services;

/// <summary>الحالة المشتركة بين كل الصفحات (الحساب، النسخ، الإعدادات).</summary>
public partial class AppState : ObservableObject
{
    public static AppState I { get; } = new();

    public Database Db { get; private set; } = null!;
    public SettingsService Settings { get; private set; } = null!;
    public AuthService Auth { get; } = new();
    public LauncherService Launcher { get; } = new();
    public ObservableCollection<Instance> Instances { get; } = new();

    [ObservableProperty] private Instance? _selectedInstance;
    [ObservableProperty] private Account? _account;
    public MSession? Session { get; set; }

    public void Init()
    {
        Db = new Database(Paths.DbFile);
        Settings = new SettingsService(Db);
        foreach (var i in Db.GetInstances()) Instances.Add(i);
        // آخر نسخة تم تشغيلها (Last Played)
        SelectedInstance = Instances.FirstOrDefault(i => i.Id == Settings.LastInstanceId)
                           ?? Instances.OrderByDescending(i => i.LastPlayed).FirstOrDefault();
    }

    public void SetSession(MSession session, string type, bool remember)
    {
        Session = session;
        var acc = new Account { Username = session.Username ?? "", Uuid = session.UUID ?? "", Type = type };
        acc.Id = Db.UpsertAccount(acc);
        Account = acc;
        Settings.Remember = remember;
        Settings.LastAccountId = acc.Id;
    }

    public async Task<bool> TryAutoLoginAsync()
    {
        try
        {
            if (!Settings.Remember) return false;
            var acc = Db.GetAccounts().FirstOrDefault(a => a.Id == Settings.LastAccountId);
            if (acc == null) return false;
            if (acc.Type == "Offline") { SetSession(AuthService.Offline(acc.Username), "Offline", true); return true; }
            var s = await Auth.MicrosoftSilentAsync();
            SetSession(s, "Microsoft", true);
            return true;
        }
        catch { return false; }
    }

    public async Task LogoutAsync()
    {
        await Auth.SignOutAsync();
        Session = null; Account = null;
        Settings.Remember = false;
    }
}
