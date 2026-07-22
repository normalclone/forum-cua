using Forum.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Forum.Web.Services;

/// <summary>Chốt chặn trước khi đăng: tạm cấm nói (mute) + giới hạn tần suất (rate limit).
/// Trả về thông báo lỗi (tiếng Việt) nếu bị chặn, hoặc null nếu được phép.</summary>
public interface IPostingGuardService
{
    Task<string?> CheckTopicAsync(int userId, bool isStaff);
    Task<string?> CheckCommentAsync(int userId, bool isStaff);
    Task<string?> MuteReasonAsync(int userId);
}

public class PostingGuardService : IPostingGuardService
{
    private readonly ApplicationDbContext _db;
    private readonly ISiteSettingService _settings;
    private readonly IMemoryCache _cache;

    public PostingGuardService(ApplicationDbContext db, ISiteSettingService settings, IMemoryCache cache)
    {
        _db = db; _settings = settings; _cache = cache;
    }

    public async Task<string?> MuteReasonAsync(int userId)
    {
        var until = await _db.Users.Where(u => u.Id == userId).Select(u => u.MutedUntil).FirstOrDefaultAsync();
        if (until.HasValue && until.Value > DateTimeOffset.UtcNow)
            return $"Bạn đang bị tạm cấm đăng bài đến {until.Value.ToLocalTime():HH:mm dd/MM/yyyy}.";
        return null;
    }

    public async Task<string?> CheckTopicAsync(int userId, bool isStaff)
    {
        if (isStaff) return null;
        var mute = await MuteReasonAsync(userId);
        if (mute != null) return mute;
        var limit = _settings.GetInt(SettingKeys.RateTopicsPerHour, 0);
        return Hit($"rl:t:{userId}", limit, TimeSpan.FromHours(1))
            ? "Bạn đăng chủ đề quá nhanh. Vui lòng thử lại sau ít phút." : null;
    }

    public async Task<string?> CheckCommentAsync(int userId, bool isStaff)
    {
        if (isStaff) return null;
        var mute = await MuteReasonAsync(userId);
        if (mute != null) return mute;
        var limit = _settings.GetInt(SettingKeys.RateCommentsPerMinute, 0);
        return Hit($"rl:c:{userId}", limit, TimeSpan.FromMinutes(1))
            ? "Bạn bình luận quá nhanh. Vui lòng chậm lại một chút." : null;
    }

    /// <summary>Sliding window đơn giản trong bộ nhớ: nếu số lần trong cửa sổ ≥ limit → chặn, ngược lại ghi nhận.
    /// limit ≤ 0 nghĩa là tắt giới hạn.</summary>
    private bool Hit(string key, int limit, TimeSpan window)
    {
        if (limit <= 0) return false;
        var now = DateTimeOffset.UtcNow;
        var list = _cache.GetOrCreate(key, e => { e.SlidingExpiration = window; return new List<DateTimeOffset>(); })!;
        lock (list)
        {
            list.RemoveAll(t => now - t > window);
            if (list.Count >= limit) return true;
            list.Add(now);
            return false;
        }
    }
}
