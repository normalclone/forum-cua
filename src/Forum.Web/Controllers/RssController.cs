using System.Net;
using System.Text;
using Forum.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Forum.Web.Controllers;

/// <summary>Nguồn cấp RSS 2.0: toàn diễn đàn, theo danh mục, theo thẻ. Chỉ danh mục công khai.</summary>
public class RssController : ForumControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IForumUrlService _url;
    private readonly ISiteSettingService _settings;

    public RssController(ApplicationDbContext db, IForumUrlService url, ISiteSettingService settings)
    {
        _db = db; _url = url; _settings = settings;
    }

    [HttpGet("/rss")]
    public async Task<IActionResult> Forum()
    {
        var topics = await PublicQuery().OrderByDescending(t => t.CreatedAt).Take(30).ToListAsync();
        return Feed(_settings.SiteName, _settings.SiteDescription, _url.Absolute("/"), topics);
    }

    [HttpGet("/danh-muc/{slug}/rss")]
    public async Task<IActionResult> Category(string slug)
    {
        var cat = await _db.Categories.FirstOrDefaultAsync(c => c.Slug == slug);
        if (cat is null) return NotFound();
        if (!string.IsNullOrEmpty(cat.MinRoleToView)) return NotFound();   // danh mục riêng tư: không phát RSS
        var topics = await PublicQuery().Where(t => t.CategoryId == cat.Id)
            .OrderByDescending(t => t.CreatedAt).Take(30).ToListAsync();
        return Feed($"{_settings.SiteName} — {cat.Name}", cat.Description ?? cat.Name, _url.Absolute(_url.Category(slug)), topics);
    }

    [HttpGet("/the/{slug}/rss")]
    public async Task<IActionResult> Tag(string slug)
    {
        var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Slug == slug);
        if (tag is null) return NotFound();
        var topics = await PublicQuery().Where(t => t.TopicTags.Any(tt => tt.TagId == tag.Id))
            .OrderByDescending(t => t.CreatedAt).Take(30).ToListAsync();
        return Feed($"{_settings.SiteName} — #{tag.Name}", $"Chủ đề gắn thẻ #{tag.Name}", _url.Absolute(_url.Tag(slug)), topics);
    }

    private IQueryable<Topic> PublicQuery()
        => _db.Topics.Include(t => t.Author)
            .Where(t => t.IsApproved && t.Category.MinRoleToView == "");

    private ContentResult Feed(string title, string description, string link, List<Topic> topics)
    {
        static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<rss version=\"2.0\"><channel>");
        sb.Append($"<title>{E(title)}</title><link>{E(link)}</link><description>{E(description)}</description>");
        foreach (var t in topics)
        {
            var url = _url.Absolute(_url.Topic(t));
            var excerpt = t.Body.Length > 300 ? t.Body[..300] + "…" : t.Body;
            sb.Append("<item>");
            sb.Append($"<title>{E(t.Title)}</title>");
            sb.Append($"<link>{E(url)}</link><guid isPermaLink=\"true\">{E(url)}</guid>");
            sb.Append($"<dc:creator xmlns:dc=\"http://purl.org/dc/elements/1.1/\">{E(t.Author?.DisplayName)}</dc:creator>");
            sb.Append($"<pubDate>{t.CreatedAt.ToUniversalTime():r}</pubDate>");
            sb.Append($"<description>{E(excerpt)}</description>");
            sb.Append("</item>");
        }
        sb.Append("</channel></rss>");
        return Content(sb.ToString(), "application/rss+xml; charset=utf-8");
    }
}
