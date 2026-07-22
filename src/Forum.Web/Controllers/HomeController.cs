using System.Diagnostics;
using Forum.Web.Models.ViewModels;
using Forum.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Forum.Web.Controllers;

public class HomeController : ForumControllerBase
{
    private readonly ISearchService _search;
    private readonly ISeoService _seo;
    private readonly IForumUrlService _url;

    public HomeController(ISearchService search, ISeoService seo, IForumUrlService url)
    {
        _search = search; _seo = seo; _url = url;
    }

    [HttpGet("/")]
    public async Task<IActionResult> Index([FromQuery(Name = "sap-xep")] string? sapXep,
        [FromQuery(Name = "thoi-gian")] string? thoiGian, [FromQuery(Name = "trang")] int trang = 1)
    {
        var (sort, sortKey) = ParseSort(sapXep);
        var featured = await _search.FeaturedAsync(6);
        var feed = await _search.QueryAsync(new TopicQuery
        {
            Sort = sort, Period = thoiGian, Page = trang, PageSize = 15, PinnedFirst = true, ExcludeFeatured = true
        });

        var query = BuildQuery(sortKey, thoiGian);
        var canonical = trang <= 1 && string.IsNullOrEmpty(query)
            ? _url.Absolute("/")
            : _url.Absolute(query.Length > 0 ? $"/?{query}{(trang > 1 ? $"&trang={trang}" : "")}" : $"/?trang={trang}");

        var seo = new SeoModel
        {
            Title = _seo.SiteName,
            Description = "Cộng đồng thảo luận về cửa: cửa gỗ, nhôm kính, cửa cuốn, uPVC, cửa chống cháy, phụ kiện, báo giá & phong thủy cửa.",
            CanonicalUrl = canonical,
            PrevUrl = feed.HasPrevious ? _url.Absolute(BuildPageUrl("/", query, trang - 1)) : null,
            NextUrl = feed.HasNext ? _url.Absolute(BuildPageUrl("/", query, trang + 1)) : null
        };
        seo.JsonLd.Add(_seo.WebSiteJsonLd());
        SetSeo(seo);

        if (IsAjax)
            return PartialView("_TopicFeed", new TopicFeedModel
            {
                Items = feed,
                BasePath = "/",
                PagerQuery = sortKey == "hoat-dong" ? "" : $"sap-xep={sortKey}",
                EmptyIcon = "door-open",
                EmptyTitle = "Chưa có chủ đề nào",
                EmptyText = "Hãy là người đầu tiên mở màn thảo luận!",
                EmptyCta = "Tạo chủ đề đầu tiên"
            });

        return View(new HomeViewModel { Featured = featured, Feed = feed, Sort = sort, Period = thoiGian });
    }

    [HttpGet("/loi")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        SetSeo(new SeoModel { Title = "Đã xảy ra lỗi", NoIndex = true });
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    internal static (TopicSort sort, string key) ParseSort(string? s) => s switch
    {
        "moi" => (TopicSort.Latest, "moi"),
        "noi-bat" => (TopicSort.Top, "noi-bat"),
        "xu-huong" => (TopicSort.Trending, "xu-huong"),
        _ => (TopicSort.Active, "hoat-dong")
    };

    private static string BuildQuery(string sortKey, string? period)
    {
        var parts = new List<string>();
        if (sortKey != "hoat-dong") parts.Add($"sap-xep={sortKey}");
        if (!string.IsNullOrEmpty(period)) parts.Add($"thoi-gian={period}");
        return string.Join("&", parts);
    }

    private static string BuildPageUrl(string path, string query, int page)
    {
        if (page <= 1) return query.Length > 0 ? $"{path}?{query}" : path;
        return query.Length > 0 ? $"{path}?{query}&trang={page}" : $"{path}?trang={page}";
    }
}
