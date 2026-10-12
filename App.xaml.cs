using System.Windows;
using GmuLauncher.Views;

namespace GmuLauncher;

public partial class App : Application
{
    /// <summary>true أثناء تبديل الحساب كي لا يُغلق التطبيق عند إغلاق النافذة الرئيسية.</summary>
    public static bool Switching;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += (_, a) =>
        {
            MessageBox.Show(a.Exception.Message, "Gmu Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
            a.Handled = true;
        };

        try
        {
            Paths.EnsureAll();
            AppState.I.Init();
            Loc.Apply(AppState.I.Settings.Lang);

            var splash = new SplashWindow();
            splash.Show();

            // نشغّل أنيميشن الشاشة وتسجيل الدخول التلقائي بالتوازي
            var auto = AppState.I.TryAutoLoginAsync();
            await Task.WhenAll(splash.PlayAsync(), auto);
            splash.Close();

            if (!await auto && !ShowLogin()) { Shutdown(); return; }
            OpenMain();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Gmu Launcher");
            Shutdown();
        }
    }

    public static bool ShowLogin() => new LoginWindow().ShowDialog() == true;

    static void OpenMain()
    {
        var m = new MainWindow();
        Current.MainWindow = m;
        m.Show();
    }

    /// <summary>تبديل الحساب: نخفي النافذة الرئيسية ونعرض شاشة الدخول.</summary>
    public static void SwitchAccount()
    {
        Switching = true;
        var old = Current.MainWindow;
        old?.Hide();
        if (ShowLogin()) { old?.Close(); Switching = false; OpenMain(); }
        else { old?.Show(); Switching = false; }
    }

    /// <summary>تسجيل الخروج الكامل (مسح الجلسة ثم شاشة الدخول).</summary>
    public static async Task LogoutAsync()
    {
        Switching = true;
        var old = Current.MainWindow;
        old?.Hide();
        await AppState.I.LogoutAsync();
        if (ShowLogin()) { old?.Close(); Switching = false; OpenMain(); }
        else { Current.Shutdown(); }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // إن لم يختر المستخدم "تذكرني" نمسح جلسة Microsoft المخزنة
        try
        {
            if (!AppState.I.Settings.Remember)
                Task.Run(() => AppState.I.Auth.SignOutAsync()).Wait(3000);
        }
        catch { }
        base.OnExit(e);
    }
}
