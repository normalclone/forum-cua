using Forum.Web.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Forum.Web.ViewComponents;

public class ForumSidebarViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;

    public ForumSidebarViewComponent(ApplicationDbContext db, IMemoryCache cache)
    {
        _db = db; _cache = cache;
    }

    /// <param name="part">"categories" (rail trái), "info" (rail phải: thống kê/thành viên/nội quy), hoặc "all".</param>
    public async Task<IViewComponentResult> InvokeAsync(string? active = null, string part = "all")
    {
        var vm = await _cache.GetOrCreateAsync("sidebar", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            var cats = await _db.Categories.OrderBy(c => c.DisplayOrder).ToListAsync();
            var counts = await _db.Topics.Where(t => !t.IsDeleted)
                .GroupBy(t => t.CategoryId).Select(g => new { g.Key, C = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.C);
            var top = await _db.Users.OrderByDescending(u => u.Reputation).Take(6).ToListAsync();
            var tags = await _db.Tags.OrderByDescending(t => t.UseCount).ThenBy(t => t.Name).Take(20).ToListAsync();
            return new SidebarViewModel
            {
                Categories = cats.Select(c => new CategoryCount(c, counts.GetValueOrDefault(c.Id))).ToList(),
                Tags = tags,
                TopMembers = top,
                TotalTopics = await _db.Topics.CountAsync(t => !t.IsDeleted),
                TotalMembers = await _db.Users.CountAsync(),
                TotalComments = await _db.Comments.CountAsync(c => !c.IsDeleted)
            };
        });

        // Truyền active/part qua ViewData để không phải sửa đối tượng đang cache.
        ViewData["Active"] = active;
        ViewData["Part"] = part;
        return View(vm!);
    }
}
