using Microsoft.Data.Sqlite;

namespace GmuLauncher.Services;

/// <summary>طبقة SQLite بسيطة: الحسابات، النسخ، حالة المودات، سجل التحميلات، الإعدادات.</summary>
public class Database
{
    readonly string _cs;

    public Database(string file)
    {
        _cs = new SqliteConnectionStringBuilder { DataSource = file }.ToString();
        Exec(@"
CREATE TABLE IF NOT EXISTS Settings(Key TEXT PRIMARY KEY, Value TEXT);
CREATE TABLE IF NOT EXISTS Accounts(Id INTEGER PRIMARY KEY AUTOINCREMENT, Username TEXT NOT NULL, Uuid TEXT, Type TEXT NOT NULL, UNIQUE(Username, Type));
CREATE TABLE IF NOT EXISTS Instances(Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, FolderName TEXT NOT NULL, McVersion TEXT, VersionType TEXT, Loader TEXT, LaunchVersion TEXT, RamMb INTEGER DEFAULT 0, LastPlayed TEXT);
CREATE TABLE IF NOT EXISTS InstanceMods(InstanceId INTEGER, FileName TEXT, Enabled INTEGER, PRIMARY KEY(InstanceId, FileName));
CREATE TABLE IF NOT EXISTS Downloads(Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT, Source TEXT, Url TEXT, Path TEXT, Date TEXT, Status TEXT);");
    }

    SqliteConnection Open() { var c = new SqliteConnection(_cs); c.Open(); return c; }

    void Exec(string sql, params (string n, object? v)[] ps)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var p in ps) cmd.Parameters.AddWithValue(p.n, p.v ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    List<T> Query<T>(string sql, Func<SqliteDataReader, T> map, params (string n, object? v)[] ps)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var p in ps) cmd.Parameters.AddWithValue(p.n, p.v ?? DBNull.Value);
        using var r = cmd.ExecuteReader();
        var list = new List<T>();
        while (r.Read()) list.Add(map(r));
        return list;
    }

    static string S(SqliteDataReader r, int i) => r.IsDBNull(i) ? "" : r.GetString(i);

    // ---------- الإعدادات ----------
    public string? GetSetting(string key) =>
        Query("SELECT Value FROM Settings WHERE Key=@k", r => S(r, 0), ("@k", key)).FirstOrDefault();

    public void SetSetting(string key, string value) =>
        Exec("INSERT INTO Settings(Key,Value) VALUES(@k,@v) ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value", ("@k", key), ("@v", value));

    // ---------- الحسابات ----------
    public long UpsertAccount(Account a)
    {
        Exec("INSERT INTO Accounts(Username,Uuid,Type) VALUES(@u,@i,@t) ON CONFLICT(Username,Type) DO UPDATE SET Uuid=excluded.Uuid",
            ("@u", a.Username), ("@i", a.Uuid), ("@t", a.Type));
        return Query("SELECT Id FROM Accounts WHERE Username=@u AND Type=@t", r => r.GetInt64(0), ("@u", a.Username), ("@t", a.Type)).First();
    }

    public List<Account> GetAccounts() =>
        Query("SELECT Id,Username,Uuid,Type FROM Accounts ORDER BY Id DESC",
            r => new Account { Id = r.GetInt64(0), Username = S(r, 1), Uuid = S(r, 2), Type = S(r, 3) });

    public void DeleteAccount(long id) => Exec("DELETE FROM Accounts WHERE Id=@i", ("@i", id));

    // ---------- النسخ ----------
    public List<Instance> GetInstances() =>
        Query("SELECT Id,Name,FolderName,McVersion,VersionType,Loader,LaunchVersion,RamMb,LastPlayed FROM Instances ORDER BY Id", r => new Instance
        {
            Id = r.GetInt64(0), Name = S(r, 1), FolderName = S(r, 2), McVersion = S(r, 3), VersionType = S(r, 4),
            Loader = S(r, 5), LaunchVersion = S(r, 6), RamMb = r.IsDBNull(7) ? 0 : r.GetInt32(7),
            LastPlayed = DateTime.TryParse(S(r, 8), out var d) ? d : null
        });

    public void SaveInstance(Instance i)
    {
        var lp = i.LastPlayed?.ToString("o");
        if (i.Id == 0)
        {
            Exec("INSERT INTO Instances(Name,FolderName,McVersion,VersionType,Loader,LaunchVersion,RamMb,LastPlayed) VALUES(@n,@f,@m,@t,@l,@lv,@r,@p)",
                ("@n", i.Name), ("@f", i.FolderName), ("@m", i.McVersion), ("@t", i.VersionType), ("@l", i.Loader), ("@lv", i.LaunchVersion), ("@r", i.RamMb), ("@p", lp));
            i.Id = Query("SELECT last_insert_rowid()", r => r.GetInt64(0)).First();
        }
        else
        {
            Exec("UPDATE Instances SET Name=@n,McVersion=@m,VersionType=@t,Loader=@l,LaunchVersion=@lv,RamMb=@r,LastPlayed=@p WHERE Id=@i",
                ("@n", i.Name), ("@m", i.McVersion), ("@t", i.VersionType), ("@l", i.Loader), ("@lv", i.LaunchVersion), ("@r", i.RamMb), ("@p", lp), ("@i", i.Id));
        }
    }

    public void DeleteInstance(long id)
    {
        Exec("DELETE FROM Instances WHERE Id=@i", ("@i", id));
        Exec("DELETE FROM InstanceMods WHERE InstanceId=@i", ("@i", id));
    }

    // ---------- حالة المودات لكل نسخة ----------
    public void SetModState(long instanceId, string fileName, bool enabled) =>
        Exec("INSERT INTO InstanceMods(InstanceId,FileName,Enabled) VALUES(@i,@f,@e) ON CONFLICT(InstanceId,FileName) DO UPDATE SET Enabled=excluded.Enabled",
            ("@i", instanceId), ("@f", fileName), ("@e", enabled ? 1 : 0));

    // ---------- سجل التحميلات ----------
    public void AddDownload(string name, string source, string url, string path, string status) =>
        Exec("INSERT INTO Downloads(Name,Source,Url,Path,Date,Status) VALUES(@n,@s,@u,@p,@d,@t)",
            ("@n", name), ("@s", source), ("@u", url), ("@p", path), ("@d", DateTime.Now.ToString("o")), ("@t", status));
}
