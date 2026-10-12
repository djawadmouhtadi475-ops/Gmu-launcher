using System.Windows.Media.Imaging;

namespace GmuLauncher.Models;

/// <summary>نسخة لعبة مخصصة (لها مجلد خاص بها).</summary>
public partial class Instance : ObservableObject
{
    public long Id { get; set; }
    [ObservableProperty] private string _name = "";
    public string FolderName { get; set; } = "";
    public string McVersion { get; set; } = "";
    public string VersionType { get; set; } = "release";
    public string Loader { get; set; } = "Vanilla";
    /// <summary>المعرّف الذي سيُشغَّل فعلياً (مثلاً fabric-loader-0.15.11-1.20.1).</summary>
    public string LaunchVersion { get; set; } = "";
    /// <summary>0 = استخدم الإعداد العام.</summary>
    public int RamMb { get; set; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(LastPlayedText))] private DateTime? _lastPlayed;

    // المسار المطلوب: .minecraft/versions/[اسم-النسخة]
    public string Folder => Path.Combine(Paths.Minecraft, "versions", FolderName);
    public string ModsFolder => Path.Combine(Folder, "mods");
    public string PacksFolder => Path.Combine(Folder, "resourcepacks");
    public string Display => $"{Name}  ·  {Loader} {McVersion}";
    public string LastPlayedText => LastPlayed is { } d ? d.ToString("yyyy-MM-dd HH:mm") : Loc.T("Inst_Never");
}

public class Account
{
    public long Id { get; set; }
    public string Username { get; set; } = "";
    public string Uuid { get; set; } = "";
    public string Type { get; set; } = "Offline";   // Microsoft | Offline
    public string AvatarUrl => $"https://mc-heads.net/avatar/{(string.IsNullOrEmpty(Uuid) ? Username : Uuid)}/64";
}

public class VersionInfo
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "release";
    public DateTime ReleaseTime { get; set; }
}

public class ModResult
{
    public string Source { get; set; } = "";
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Author { get; set; } = "";
    public long Downloads { get; set; }
    public string? IconUrl { get; set; }
    public string DownloadsText => Downloads switch
    {
        >= 1_000_000 => $"{Downloads / 1_000_000.0:0.#}M",
        >= 1_000 => $"{Downloads / 1_000.0:0.#}K",
        _ => Downloads.ToString()
    };
}

/// <summary>مود محلي داخل مجلد mods.</summary>
public class LocalMod : ObservableObject
{
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Description { get; set; } = "";
    public string Loader { get; set; } = "";
    public string? McRange { get; set; }
    public BitmapImage? Logo { get; set; }

    string _warning = "";
    public string Warning { get => _warning; set { SetProperty(ref _warning, value); OnPropertyChanged(nameof(HasWarning)); } }
    public bool HasWarning => !string.IsNullOrEmpty(Warning);

    bool _isEnabled; bool _loaded;
    public event Action<LocalMod>? Toggled;
    public bool IsEnabled
    {
        get => _isEnabled;
        set { if (SetProperty(ref _isEnabled, value) && _loaded) Toggled?.Invoke(this); }
    }
    public void InitEnabled(bool v) { _isEnabled = v; _loaded = true; }
}

public class PackItem : ObservableObject
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public BitmapImage? Thumb { get; set; }
    bool _isEnabled; bool _loaded;
    public event Action<PackItem>? Toggled;
    public bool IsEnabled
    {
        get => _isEnabled;
        set { if (SetProperty(ref _isEnabled, value) && _loaded) Toggled?.Invoke(this); }
    }
    public void InitEnabled(bool v) { _isEnabled = v; _loaded = true; }
}

public class SkinItem
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public BitmapSource? Head { get; set; }

    public static SkinItem FromFile(string path)
    {
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.UriSource = new Uri(path);
        bmp.CacheOption = BitmapCacheOption.OnLoad;   // لا نُبقي الملف مقفلاً
        bmp.EndInit();
        bmp.Freeze();
        BitmapSource? head = null;
        if (bmp.PixelWidth >= 16 && bmp.PixelHeight >= 16)
        {
            var c = new CroppedBitmap(bmp, new System.Windows.Int32Rect(8, 8, 8, 8));
            c.Freeze();
            head = c;
        }
        return new SkinItem { Name = System.IO.Path.GetFileNameWithoutExtension(path), Path = path, Head = head };
    }
}

public record LangItem(string Code, string Name);
