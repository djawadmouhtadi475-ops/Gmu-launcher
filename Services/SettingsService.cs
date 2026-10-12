namespace GmuLauncher.Services;

public class SettingsService
{
    readonly Database _db;
    public SettingsService(Database db) => _db = db;

    string Str(string k, string def) => _db.GetSetting(k) ?? def;
    int Int(string k, int def) => int.TryParse(_db.GetSetting(k), out var v) ? v : def;
    long Long(string k, long def) => long.TryParse(_db.GetSetting(k), out var v) ? v : def;

    public int RamMb { get => Int("ram", 4096); set => _db.SetSetting("ram", value.ToString()); }
    public string JavaPath { get => Str("java", ""); set => _db.SetSetting("java", value); }
    public string JvmArgs { get => Str("jvm", ""); set => _db.SetSetting("jvm", value); }
    public int ResW { get => Int("resw", 0); set => _db.SetSetting("resw", value.ToString()); }
    public int ResH { get => Int("resh", 0); set => _db.SetSetting("resh", value.ToString()); }
    public string CurseForgeKey { get => Str("cfkey", ""); set => _db.SetSetting("cfkey", value); }
    public string Lang { get => Str("lang", "ar"); set => _db.SetSetting("lang", value); }
    public bool Remember { get => Int("remember", 0) == 1; set => _db.SetSetting("remember", value ? "1" : "0"); }
    public long LastAccountId { get => Long("lastacc", 0); set => _db.SetSetting("lastacc", value.ToString()); }
    public long LastInstanceId { get => Long("lastinst", 0); set => _db.SetSetting("lastinst", value.ToString()); }
}
