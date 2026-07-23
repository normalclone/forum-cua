using Forum.Web.Helpers;
using Forum.Web.Models.ViewModels;
using Forum.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Forum.Web.Controllers;

public class CategoriesController : ForumControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ISearchService _search;
    private readonly ISeoService _seo;
    private readonly IForumUrlService _url;

    public CategoriesController(ApplicationDbContext db, ISearchService search, ISeoService seo, IForumUrlService url)
    {
        _db = db; _search = search; _seo = seo; _url = url;
    }

    [HttpGet("/danh-muc")]
    public async Task<IActionResult> Index()
    {
        var cats = await _db.Categories.OrderBy(c => c.DisplayOrder).ToListAsync();
        var counts = await _db.Topics.Where(t => !t.IsDeleted).GroupBy(t => t.CategoryId)
            .Select(g => new { g.Key, C = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.C);
        ViewData["Counts"] = counts;
        SetSeo(new SeoModel
        {
            Title = "Danh mục thảo luận",
            Description = "Tất cả danh mục: cửa gỗ, nhôm kính, cửa cuốn, uPVC, chống cháy, phụ kiện, báo giá, phong thủy.",
            CanonicalUrl = _url.Absolute("/danh-muc"),
            Breadcrumbs = { new BreadcrumbItem("Trang chủ", "/"), new BreadcrumbItem("Danh mục", null) }
        });
        return View(cats);
    }

    [HttpGet("/danh-muc/{slug}")]
    public async Task<IActionResult> Detail(string slug, [FromQuery(Name = "sap-xep")] string? sapXep, [FromQuery(Name = "trang")] int trang = 1)
    {
        var cat = await _db.Categories.FirstOrDefaultAsync(c => c.Slug == slug);
        if (cat is null) return NotFound();
        if (!CategoryAccess.CanView(User, cat.MinRoleToView)) return NotFound();   // danh mục riêng tư

        var (sort, sortKey) = HomeController.ParseSort(sapXep);
        // Bài ghim/nổi bật của riêng danh mục → strip đầu trang. Loại đúng các bài này
        // khỏi feed bên dưới (theo Id) để không trùng; tính trên MỌI trang để phân trang
        // nhất quán (strip chỉ render ở trang 1, xem Listing.cshtml).
        var featured = await _search.FeaturedAsync(4, cat.Id);
        var topics = await _search.QueryAsync(new TopicQuery
        {
            CategoryId = cat.Id, Sort = sort, Page = trang, PageSize = 20,
            PinnedFirst = true, ExcludeIds = featured.Select(t => t.Id).ToArray()
        });

        var query = sortKey == "hoat-dong" ? "" : $"sap-xep={sortKey}";
        var basePath = _url.Category(slug);
        SetSeo(new SeoModel
        {
            Title = cat.Name,
            Description = cat.Description ?? $"Thảo luận về {cat.Name} trên Diễn đàn Cửa.",
            CanonicalUrl = _url.Absolute(PageUrl(basePath, query, trang)),
            PrevUrl = topics.HasPrevious ? _url.Absolute(PageUrl(basePath, query, trang - 1)) : null,
            NextUrl = topics.HasNext ? _url.Absolute(PageUrl(basePath, query, trang + 1)) : null,
            Breadcrumbs = { new BreadcrumbItem("Trang chủ", "/"), new BreadcrumbItem("Danh mục", "/danh-muc"), new BreadcrumbItem(cat.Name, null) }
        });

        if (IsAjax)
            return PartialView("_TopicFeed", new TopicFeedModel
            {
                Items = topics,
                BasePath = basePath,
                PagerQuery = sortKey == "hoat-dong" ? "" : $"sap-xep={sortKey}",
                EmptyIcon = "folder",
                EmptyTitle = "Chưa có chủ đề",
                EmptyText = "Hãy mở màn thảo luận trong mục này."
            });

        return View("Listing", new TopicListViewModel
        {
            Topics = topics, Featured = featured, Category = cat, Sort = sort, Heading = cat.Name, BasePath = basePath
        });
    }

    internal static string PageUrl(string path, string query, int page)
    {
        if (page <= 1) return query.Length > 0 ? $"{path}?{query}" : path;
        return query.Length > 0 ? $"{path}?{query}&trang={page}" : $"{path}?trang={page}";
    }
}
