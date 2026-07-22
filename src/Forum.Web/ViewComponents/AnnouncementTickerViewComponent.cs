using Forum.Web.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Forum.Web.ViewComponents;

/// <summary>Thanh thông báo chạy (marquee): ưu tiên thông báo do admin soạn,
/// nếu không có thì lấy chủ đề ghim + nổi bật.</summary>
public class AnnouncementTickerViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;

    public AnnouncementTickerViewComponent(ApplicationDbContext db, IMemoryCache cache)
    {
        _db = db; _cache = cache;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var items = await _cache.GetOrCreateAsync("ticker-items", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
            // StartsAt/EndsAt được admin nhập bằng <input type="datetime-local"> = giờ ĐỊA PHƯƠNG,
            // nên phải so với DateTime.Now (giờ local) chứ không phải UtcNow — nếu không lịch hẹn lệch theo múi giờ.
            var now = DateTime.Now;

            // 1) Thông báo do admin soạn (đang hiệu lực).
            var anns = await _db.Announcements
                .Where(a => a.IsActive)
                .OrderBy(a => a.DisplayOrder).ThenByDescending(a => a.CreatedAt)
                .ToListAsync();
            var live = anns
                .Where(a => (a.StartsAt == null || a.StartsAt <= now) && (a.EndsAt == null || a.EndsAt >= now))
                .Select(a => new TickerItem(a.Message, string.IsNullOrWhiteSpace(a.Url) ? null : a.Url, "bell"))
                .ToList();
            if (live.Count > 0) return live;

            // 2) Fallback: chủ đề ghim + nổi bật.
            return await _db.Topics
                .Where(t => !t.IsDeleted && t.IsApproved && (t.IsPinned || t.IsFeatured))
                .OrderByDescending(t => t.IsPinned).ThenByDescending(t => t.HotScore)
                .Take(8)
                .Select(t => new TickerItem(t.Title, $"/chu-de/{t.Id}/{t.Slug}", t.IsPinned ? "pin" : "flame"))
                .ToListAsync();
        });
        return View(items ?? new List<TickerItem>());
    }
}
