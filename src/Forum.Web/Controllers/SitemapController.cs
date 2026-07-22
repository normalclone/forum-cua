using System.Text;
using System.Xml;
using Forum.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Forum.Web.Controllers;

/// <summary>Sinh sitemap.xml động và robots.txt cho SEO.</summary>
public class SitemapController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IForumUrlService _url;

    public SitemapController(ApplicationDbContext db, IForumUrlService url)
    {
        _db = db; _url = url;
    }

    [HttpGet("/sitemap.xml")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Sitemap()
    {
        var sb = new StringBuilder();
        var settings = new XmlWriterSettings { Async = false, Indent = true, Encoding = new UTF8Encoding(false) };
        await using var sw = new StringWriter(sb);
        using (var w = XmlWriter.Create(sw, settings))
        {
            w.WriteStartDocument();
            w.WriteStartElement("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");

            void Url(string loc, DateTime? lastMod, string freq, string priority)
            {
                w.WriteStartElement("url");
                w.WriteElementString("loc", _url.Absolute(loc));
                if (lastMod.HasValue) w.WriteElementString("lastmod", lastMod.Value.ToString("yyyy-MM-dd"));
                w.WriteElementString("changefreq", freq);
                w.WriteElementString("priority", priority);
                w.WriteEndElement();
            }

            Url("/", DateTime.UtcNow, "hourly", "1.0");
            Url("/danh-muc", DateTime.UtcNow, "daily", "0.7");
            Url("/the", DateTime.UtcNow, "weekly", "0.5");
            Url("/thanh-vien-tich-cuc", DateTime.UtcNow, "weekly", "0.4");

            foreach (var c in await _db.Categories.OrderBy(c => c.DisplayOrder).ToListAsync())
                Url(_url.Category(c.Slug), null, "daily", "0.8");

            var topics = await _db.Topics.Where(t => !t.IsDeleted)
                .OrderByDescending(t => t.LastActivityAt)
                .Select(t => new { t.Id, t.Slug, t.LastActivityAt }).ToListAsync();
            foreach (var t in topics)
                Url(_url.Topic(t.Id, t.Slug), t.LastActivityAt, "weekly", "0.7");

            foreach (var tag in await _db.Tags.OrderByDescending(t => t.UseCount).Take(100).ToListAsync())
                Url(_url.Tag(tag.Slug), null, "weekly", "0.4");

            w.WriteEndElement();
            w.WriteEndDocument();
        }
        return Content(sb.ToString(), "application/xml", Encoding.UTF8);
    }

    [HttpGet("/robots.txt")]
    public IActionResult Robots()
    {
        var sb = new StringBuilder();
        sb.AppendLine("User-agent: *");
        sb.AppendLine("Allow: /");
        foreach (var p in new[] { "/cai-dat", "/tin-nhan", "/tim-kiem", "/thong-bao", "/kiem-duyet", "/dang-nhap", "/dang-ky", "/bang-tin", "/da-luu", "/nhap" })
            sb.AppendLine($"Disallow: {p}");
        sb.AppendLine();
        sb.AppendLine($"Sitemap: {_url.Absolute("/sitemap.xml")}");
        return Content(sb.ToString(), "text/plain", Encoding.UTF8);
    }
}
