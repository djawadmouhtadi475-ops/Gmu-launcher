using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace GmuLauncher.ViewModels;

public partial class SkinsViewModel : ObservableObject
{
    public AppState State => AppState.I;
    public ObservableCollection<SkinItem> Skins { get; } = new();

    [ObservableProperty] private SkinItem? _selectedSkin;
    [ObservableProperty] private bool _slim;
    [ObservableProperty] private string _playerName = "";
    [ObservableProperty] private string _message = "";
    [ObservableProperty] private bool _isBusy;

    public SkinsViewModel()
    {
        Directory.CreateDirectory(Paths.Skins);
        foreach (var f in Directory.GetFiles(Paths.Skins, "*.png"))
            try { Skins.Add(SkinItem.FromFile(f)); } catch { }
        SelectedSkin = Skins.FirstOrDefault();
    }

    [RelayCommand]
    private void Import()
    {
        var dlg = new OpenFileDialog { Filter = "Skin (*.png)|*.png", Multiselect = true };
        if (dlg.ShowDialog() != true) return;
        foreach (var f in dlg.FileNames) AddFile(f);
    }

    void AddFile(string src)
    {
        var dest = Path.Combine(Paths.Skins, Path.GetFileName(src));
        if (!string.Equals(src, dest, StringComparison.OrdinalIgnoreCase)) File.Copy(src, dest, true);
        var item = SkinItem.FromFile(dest);
        var old = Skins.FirstOrDefault(s => s.Path == dest);
        if (old != null) Skins.Remove(old);
        Skins.Add(item);
        SelectedSkin = item;
    }

    /// <summary>استيراد skin لاعب حقيقي: اسم → UUID → ملف textures من sessionserver.</summary>
    [RelayCommand]
    private async Task ImportByNameAsync()
    {
        var name = PlayerName.Trim();
        if (name == "") return;
        Message = ""; IsBusy = true;
        try
        {
            using var u = JsonDocument.Parse(await Net.Http.GetStringAsync($"https://api.mojang.com/users/profiles/minecraft/{Uri.EscapeDataString(name)}"));
            var id = u.RootElement.GetProperty("id").GetString();
            using var p = JsonDocument.Parse(await Net.Http.GetStringAsync($"https://sessionserver.mojang.com/session/minecraft/profile/{id}"));
            var b64 = p.RootElement.GetProperty("properties")[0].GetProperty("value").GetString()!;
            using var tex = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(b64)));
            var url = tex.RootElement.GetProperty("textures").GetProperty("SKIN").GetProperty("url").GetString()!;
            var dest = Path.Combine(Paths.Skins, name + ".png");
            await Net.DownloadAsync(url, dest, null, CancellationToken.None);
            AddFile(dest);
        }
        catch (Exception ex) { Message = ex.Message; }
        finally { IsBusy = false; }
    }

    /// <summary>رفع الـ Skin إلى Mojang (للحسابات الرسمية فقط).</summary>
    [RelayCommand]
    private async Task ApplyAsync()
    {
        if (SelectedSkin == null) return;
        if (State.Account?.Type != "Microsoft" || State.Session?.AccessToken == null) { Message = Loc.T("Skins_OfflineNote"); return; }
        Message = ""; IsBusy = true;
        try
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(Slim ? "slim" : "classic"), "variant");
            var file = new ByteArrayContent(await File.ReadAllBytesAsync(SelectedSkin.Path));
            file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(file, "file", "skin.png");

            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.minecraftservices.com/minecraft/profile/skins") { Content = form };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", State.Session.AccessToken);
            using var resp = await Net.Http.SendAsync(req);
            Message = resp.IsSuccessStatusCode ? Loc.T("Skins_Applied") : $"{(int)resp.StatusCode}: {await resp.Content.ReadAsStringAsync()}";
        }
        catch (Exception ex) { Message = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void Delete()
    {
        if (SelectedSkin == null) return;
        try { File.Delete(SelectedSkin.Path); } catch { }
        var idx = Skins.IndexOf(SelectedSkin);
        Skins.Remove(SelectedSkin);
        SelectedSkin = Skins.Count > 0 ? Skins[Math.Min(idx, Skins.Count - 1)] : null;
    }
}
