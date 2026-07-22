using Forum.Web.Models.ViewModels;
using Forum.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Forum.Web.Controllers;

public class TagsController : ForumControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ISearchService _search;
    private readonly IForumUrlService _url;

    public TagsController(ApplicationDbContext db, ISearchService search, IForumUrlService url)
    {
        _db = db; _search = search; _url = url;
    }

    [HttpGet("/the")]
    public async Task<IActionResult> Index()
    {
        var tags = await _db.Tags.OrderByDescending(t => t.UseCount).Take(100).ToListAsync();
        SetSeo(new SeoModel
        {
            Title = "Thẻ (tags)",
            Description = "Khám phá chủ đề theo thẻ: cửa gỗ, Xingfa, kính cường lực, uPVC, chống cháy, báo giá…",
            CanonicalUrl = _url.Absolute("/the"),
            Breadcrumbs = { new BreadcrumbItem("Trang chủ", "/"), new BreadcrumbItem("Thẻ", null) }
        });
        return View(tags);
    }

    [HttpGet("/the/{slug}")]
    public async Task<IActionResult> Detail(string slug, [FromQuery(Name = "sap-xep")] string? sapXep, [FromQuery(Name = "trang")] int trang = 1)
    {
        var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Slug == slug);
        if (tag is null) return NotFound();

        var (sort, sortKey) = HomeController.ParseSort(sapXep);
        var topics = await _search.QueryAsync(new TopicQuery { TagSlug = slug, Sort = sort, Page = trang, PageSize = 20 });

        var query = sortKey == "hoat-dong" ? "" : $"sap-xep={sortKey}";
        var basePath = _url.Tag(slug);
        SetSeo(new SeoModel
        {
            Title = $"#{tag.Name}",
            Description = $"Các chủ đề được gắn thẻ #{tag.Name} trên Diễn đàn Cửa.",
            CanonicalUrl = _url.Absolute(CategoriesController.PageUrl(basePath, query, trang)),
            PrevUrl = topics.HasPrevious ? _url.Absolute(CategoriesController.PageUrl(basePath, query, trang - 1)) : null,
            NextUrl = topics.HasNext ? _url.Absolute(CategoriesController.PageUrl(basePath, query, trang + 1)) : null,
            Breadcrumbs = { new BreadcrumbItem("Trang chủ", "/"), new BreadcrumbItem("Thẻ", "/the"), new BreadcrumbItem($"#{tag.Name}", null) }
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
            Topics = topics, Tag = tag, Sort = sort, Heading = $"#{tag.Name}", BasePath = basePath,
            IsFollowingTag = IsAuthed && await _db.TagSubscriptions.AnyAsync(s => s.UserId == CurrentUserId && s.TagId == tag.Id)
        });
    }
}
