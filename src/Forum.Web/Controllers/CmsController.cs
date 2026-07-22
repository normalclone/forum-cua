using Forum.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Forum.Web.Controllers;

/// <summary>Trang tĩnh (CMS) do quản trị soạn — Nội quy, Giới thiệu…</summary>
public class CmsController : ForumControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IMarkdownService _md;
    private readonly IForumUrlService _url;

    public CmsController(ApplicationDbContext db, IMarkdownService md, IForumUrlService url)
    {
        _db = db; _md = md; _url = url;
    }

    [HttpGet("/noi-quy")]
    public Task<IActionResult> Rules() => RenderAsync("noi-quy", "Nội quy diễn đàn");

    [HttpGet("/trang/{slug}")]
    public Task<IActionResult> Page(string slug) => RenderAsync(slug, null);

    private async Task<IActionResult> RenderAsync(string slug, string? fallbackTitle)
    {
        var page = await _db.CmsPages.FirstOrDefaultAsync(p => p.Slug == slug && p.IsPublished);
        if (page is null)
        {
            if (fallbackTitle is null) return NotFound();
            page = new Models.CmsPage { Slug = slug, Title = fallbackTitle, Body = "" };
        }
        SetSeo(new SeoModel
        {
            Title = page.Title,
            Description = _md.Excerpt(page.Body, 160),
            CanonicalUrl = _url.Absolute(slug == "noi-quy" ? "/noi-quy" : $"/trang/{slug}"),
            Breadcrumbs = { new BreadcrumbItem("Trang chủ", "/"), new BreadcrumbItem(page.Title, null) }
        });
        ViewData["Html"] = _md.ToHtml(page.Body);
        return View("Page", page);
    }
}
