using Forum.Web.Models.ViewModels;
using Forum.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Forum.Web.Controllers;

public class MembersController : ForumControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IForumUrlService _url;

    public MembersController(ApplicationDbContext db, IForumUrlService url)
    {
        _db = db; _url = url;
    }

    [HttpGet("/thanh-vien-tich-cuc")]
    public async Task<IActionResult> Index()
    {
        var top = await _db.Users.OrderByDescending(u => u.Reputation).Take(50).ToListAsync();
        var ids = top.Select(u => u.Id).ToList();
        var topicCounts = await _db.Topics.Where(t => ids.Contains(t.AuthorId) && !t.IsDeleted)
            .GroupBy(t => t.AuthorId).Select(g => new { g.Key, C = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.C);
        var badgeCounts = await _db.UserBadges.Where(b => ids.Contains(b.UserId))
            .GroupBy(b => b.UserId).Select(g => new { g.Key, C = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.C);

        var rows = top.Select((u, i) => new LeaderRow(u, i + 1, topicCounts.GetValueOrDefault(u.Id), badgeCounts.GetValueOrDefault(u.Id))).ToList();

        SetSeo(new SeoModel
        {
            Title = "Bảng xếp hạng thành viên",
            Description = "Những thành viên tích cực nhất của Diễn đàn Cửa theo điểm uy tín.",
            CanonicalUrl = _url.Absolute("/thanh-vien-tich-cuc"),
            Breadcrumbs = { new BreadcrumbItem("Trang chủ", "/"), new BreadcrumbItem("Bảng xếp hạng", null) }
        });
        return View(rows);
    }
}
