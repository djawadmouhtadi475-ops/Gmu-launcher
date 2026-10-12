using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;

namespace GmuLauncher.Services;

/// <summary>
/// تسجيل الدخول. سلسلة Microsoft → Xbox Live → XSTS → Minecraft token
/// تتولاها مكتبة CmlLib.Core.Auth.Microsoft (التي تعتمد على XboxAuthNet).
/// الـ Refresh Token يُخزَّن بواسطة المكتبة، ونستخدمه عبر AuthenticateSilently.
/// </summary>
public class AuthService
{
    JELoginHandler? _handler;
    JELoginHandler Handler => _handler ??= JELoginHandlerBuilder.BuildDefault();

    public async Task<MSession> MicrosoftInteractiveAsync() => await Handler.AuthenticateInteractively();
    public async Task<MSession> MicrosoftSilentAsync() => await Handler.AuthenticateSilently();

    public async Task SignOutAsync()
    {
        try { await Handler.Signout(); } catch { }
    }

    /// <summary>حساب غير رسمي (Offline).</summary>
    public static MSession Offline(string username) => MSession.CreateOfflineSession(username);
}
