using System.Net.Http;
namespace GmuLauncher.Services;

public static class Net
{
    public static readonly HttpClient Http = Create();

    static HttpClient Create()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        // Modrinth تطلب User-Agent واضحاً
        c.DefaultRequestHeaders.UserAgent.ParseAdd("GmuStudios/GmuLauncher/1.0");
        return c;
    }

    /// <summary>تنزيل ملف مع نسبة التقدم (0-100) ودعم الإلغاء. يُحذف الملف الجزئي عند الفشل.</summary>
    public static async Task DownloadAsync(string url, string dest, IProgress<double>? progress, CancellationToken ct)
    {
        var tmp = dest + ".part";
        try
        {
            using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();
            var total = resp.Content.Headers.ContentLength ?? -1;
            await using (var src = await resp.Content.ReadAsStreamAsync(ct))
            await using (var fs = File.Create(tmp))
            {
                var buf = new byte[81920];
                long read = 0; int n;
                while ((n = await src.ReadAsync(buf, ct)) > 0)
                {
                    await fs.WriteAsync(buf.AsMemory(0, n), ct);
                    read += n;
                    if (total > 0) progress?.Report(read * 100.0 / total);
                }
            }
            File.Move(tmp, dest, true);
        }
        catch
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            throw;
        }
    }
}
