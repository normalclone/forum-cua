using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace Forum.Web.Services;

/// <summary>
/// Cấu hình site chỉnh sửa lúc chạy (tên site, mô tả, danh sách từ cấm).
/// Singleton + cache trong bộ nhớ; nạp từ DB lần đầu và nạp lại sau khi lưu.
/// </summary>
public interface ISiteSettingService
{
    string SiteName { get; }
    string SiteDescription { get; }
    IReadOnlyList<string> BannedWords { get; }
    Regex? BannedRegex { get; }
    string Get(string key, string fallback = "");
    bool GetBool(string key, bool fallback);
    int GetInt(string key, int fallback);
    IReadOnlyDictionary<string, string> All();
    Task SaveAsync(IDictionary<string, string> values);
}

public class SiteSettingService : ISiteSettingService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly object _lock = new();
    private Dictionary<string, string> _cache = new();
    private string[] _bannedWords = Array.Empty<string>();
    private Regex? _bannedRegex;
    private volatile bool _loaded;

    // Giá trị mặc định khi DB chưa có bản ghi tương ứng.
    public const string DefaultName = "Diễn đàn Xây dựng Việt";
    public const string DefaultDescription =
        "Cộng đồng xây dựng Việt Nam: kết cấu & thi công, vật liệu, điện nước, chống thấm, cửa nhôm kính, nội thất hoàn thiện, phong thủy, nhà thầu & báo giá.";
    private static readonly string[] DefaultBanned =
        { "đồ ngu", "thằng ngu", "lừa đảo trắng trợn", "địt", "đm", "vcl", "dmm", "óc chó" };

    public SiteSettingService(IServiceScopeFactory scopes) => _scopes = scopes;

    private void EnsureLoaded()
    {
        if (_loaded) return;
        lock (_lock)
        {
            if (_loaded) return;
            Load();
            _loaded = true;
        }
    }

    private void Load()
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        _cache = db.SiteSettings.AsNoTracking().ToDictionary(s => s.Key, s => s.Value);
        RebuildBanned();
    }

    private void RebuildBanned()
    {
        var raw = _cache.TryGetValue(SettingKeys.BannedWords, out var v) && !string.IsNullOrWhiteSpace(v)
            ? v.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : DefaultBanned;

        _bannedWords = raw.Where(w => w.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        var normalized = _bannedWords.Select(TextNormalize.ForMatch).Where(w => w.Length > 0).Distinct().ToArray();
        _bannedRegex = normalized.Length == 0
            ? null
            : new Regex(@"\b(" + string.Join("|", normalized.Select(Regex.Escape)) + @")\b",
                RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }

    public string Get(string key, string fallback = "")
    {
        EnsureLoaded();
        return _cache.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : fallback;
    }

    public bool GetBool(string key, bool fallback)
    {
        var v = Get(key, "");
        if (string.IsNullOrEmpty(v)) return fallback;
        return v is "true" or "1" or "on" or "True";
    }

    public int GetInt(string key, int fallback)
        => int.TryParse(Get(key, ""), out var n) ? n : fallback;

    public string SiteName => Get(SettingKeys.SiteName, DefaultName);
    public string SiteDescription => Get(SettingKeys.SiteDescription, DefaultDescription);

    public IReadOnlyList<string> BannedWords { get { EnsureLoaded(); return _bannedWords; } }
    public Regex? BannedRegex { get { EnsureLoaded(); return _bannedRegex; } }

    public IReadOnlyDictionary<string, string> All()
    {
        EnsureLoaded();
        return new Dictionary<string, string>(_cache);
    }

    public async Task SaveAsync(IDictionary<string, string> values)
    {
        using (var scope = _scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            foreach (var (key, value) in values)
            {
                var row = await db.SiteSettings.FindAsync(key);
                if (row is null) db.SiteSettings.Add(new SiteSetting { Key = key, Value = value ?? "" });
                else row.Value = value ?? "";
            }
            await db.SaveChangesAsync();
        }
        lock (_lock) { Load(); _loaded = true; }   // nạp lại cache + regex
    }
}
