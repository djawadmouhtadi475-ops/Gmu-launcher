using System.Text.Json;

namespace GmuLauncher.Services;

public interface IModSource
{
    string Name { get; }
    Task<List<ModResult>> SearchAsync(string query, string? mcVersion, string? loader, CancellationToken ct);
    Task<(string url, string fileName)> GetDownloadAsync(string projectId, string? mcVersion, string? loader, CancellationToken ct);
}

/// <summary>Modrinth API v2.</summary>
public class ModrinthService : IModSource
{
    public string Name => "Modrinth";
    const string Base = "https://api.modrinth.com/v2";

    public async Task<List<ModResult>> SearchAsync(string query, string? mcVersion, string? loader, CancellationToken ct)
    {
        var facets = new List<string[]> { new[] { "project_type:mod" } };
        if (!string.IsNullOrWhiteSpace(mcVersion)) facets.Add(new[] { $"versions:{mcVersion}" });
        if (!string.IsNullOrWhiteSpace(loader)) facets.Add(new[] { $"categories:{loader.ToLowerInvariant()}" });

        var url = $"{Base}/search?query={Uri.EscapeDataString(query)}&limit=24&facets={Uri.EscapeDataString(JsonSerializer.Serialize(facets))}";
        using var doc = JsonDocument.Parse(await Net.Http.GetStringAsync(url, ct));
        return doc.RootElement.GetProperty("hits").EnumerateArray().Select(h => new ModResult
        {
            Source = Name,
            Id = h.GetProperty("project_id").GetString() ?? "",
            Title = h.GetProperty("title").GetString() ?? "",
            Description = h.GetProperty("description").GetString() ?? "",
            Author = h.TryGetProperty("author", out var a) ? a.GetString() ?? "" : "",
            Downloads = h.TryGetProperty("downloads", out var d) ? d.GetInt64() : 0,
            IconUrl = h.TryGetProperty("icon_url", out var ic) && ic.ValueKind == JsonValueKind.String ? ic.GetString() : null
        }).ToList();
    }

    public async Task<(string url, string fileName)> GetDownloadAsync(string projectId, string? mcVersion, string? loader, CancellationToken ct)
    {
        var q = new List<string>();
        if (!string.IsNullOrWhiteSpace(loader)) q.Add($"loaders={Uri.EscapeDataString(JsonSerializer.Serialize(new[] { loader.ToLowerInvariant() }))}");
        if (!string.IsNullOrWhiteSpace(mcVersion)) q.Add($"game_versions={Uri.EscapeDataString(JsonSerializer.Serialize(new[] { mcVersion }))}");
        var url = $"{Base}/project/{projectId}/version" + (q.Count > 0 ? "?" + string.Join("&", q) : "");
        using var doc = JsonDocument.Parse(await Net.Http.GetStringAsync(url, ct));
        if (doc.RootElement.GetArrayLength() == 0) throw new InvalidOperationException("No compatible file found / لا يوجد ملف متوافق");
        var files = doc.RootElement[0].GetProperty("files");
        var file = files.EnumerateArray().FirstOrDefault(f => f.TryGetProperty("primary", out var p) && p.GetBoolean());
        if (file.ValueKind == JsonValueKind.Undefined) file = files[0];
        return (file.GetProperty("url").GetString()!, file.GetProperty("filename").GetString()!);
    }
}

/// <summary>CurseForge API (يحتاج API Key من console.curseforge.com).</summary>
public class CurseForgeService : IModSource
{
    public string Name => "CurseForge";
    const string Base = "https://api.curseforge.com/v1";

    static int LoaderType(string? l) => (l ?? "").ToLowerInvariant() switch
    { "forge" => 1, "fabric" => 4, "quilt" => 5, "neoforge" => 6, _ => 0 };

    async Task<JsonDocument> GetAsync(string url, CancellationToken ct)
    {
        var key = AppState.I.Settings.CurseForgeKey;
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException(Loc.T("Mods_NoKey"));
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("x-api-key", key);
        using var resp = await Net.Http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
    }

    public async Task<List<ModResult>> SearchAsync(string query, string? mcVersion, string? loader, CancellationToken ct)
    {
        var url = $"{Base}/mods/search?gameId=432&classId=6&pageSize=24&sortField=2&sortOrder=desc&searchFilter={Uri.EscapeDataString(query)}";
        if (!string.IsNullOrWhiteSpace(mcVersion)) url += $"&gameVersion={Uri.EscapeDataString(mcVersion)}";
        if (LoaderType(loader) > 0) url += $"&modLoaderType={LoaderType(loader)}";
        using var doc = await GetAsync(url, ct);
        return doc.RootElement.GetProperty("data").EnumerateArray().Select(m => new ModResult
        {
            Source = Name,
            Id = m.GetProperty("id").GetInt64().ToString(),
            Title = m.GetProperty("name").GetString() ?? "",
            Description = m.GetProperty("summary").GetString() ?? "",
            Downloads = m.TryGetProperty("downloadCount", out var d) ? (long)d.GetDouble() : 0,
            Author = m.TryGetProperty("authors", out var au) && au.GetArrayLength() > 0 ? au[0].GetProperty("name").GetString() ?? "" : "",
            IconUrl = m.TryGetProperty("logo", out var lg) && lg.ValueKind == JsonValueKind.Object ? lg.GetProperty("thumbnailUrl").GetString() : null
        }).ToList();
    }

    public async Task<(string url, string fileName)> GetDownloadAsync(string projectId, string? mcVersion, string? loader, CancellationToken ct)
    {
        var url = $"{Base}/mods/{projectId}/files?pageSize=1";
        if (!string.IsNullOrWhiteSpace(mcVersion)) url += $"&gameVersion={Uri.EscapeDataString(mcVersion)}";
        if (LoaderType(loader) > 0) url += $"&modLoaderType={LoaderType(loader)}";
        using var doc = await GetAsync(url, ct);
        var data = doc.RootElement.GetProperty("data");
        if (data.GetArrayLength() == 0) throw new InvalidOperationException("No compatible file found / لا يوجد ملف متوافق");
        var f = data[0];
        var dl = f.TryGetProperty("downloadUrl", out var u) && u.ValueKind == JsonValueKind.String ? u.GetString() : null;
        if (dl == null) throw new InvalidOperationException("The author disabled third-party downloads for this file / المطوّر لا يسمح بالتحميل الخارجي لهذا الملف");
        return (dl, f.GetProperty("fileName").GetString()!);
    }
}
