using Forum.Web.Models.ViewModels;
using Forum.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Forum.Web.Controllers;

public class SearchController : ForumControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ISearchService _search;
    private readonly IForumUrlService _url;

    public SearchController(ApplicationDbContext db, ISearchService search, IForumUrlService url)
    {
        _db = db; _search = search; _url = url;
    }

    [HttpGet("/tim-kiem")]
    public async Task<IActionResult> Index(string? q, [FromQuery(Name = "danh-muc")] string? danhMuc,
        [FromQuery(Name = "the")] string? the, [FromQuery(Name = "thoi-gian")] string? thoiGian,
        [FromQuery(Name = "sap-xep")] string? sapXep, [FromQuery(Name = "trang")] int trang = 1)
    {
        var (sort, sortKey) = sapXep == "moi" ? (TopicSort.Latest, "moi")
            : sapXep == "noi-bat" ? (TopicSort.Top, "noi-bat")
            : (TopicSort.Latest, "lien-quan"); // mặc định mới nhất khi tìm kiếm

        int? catId = null;
        if (!string.IsNullOrEmpty(danhMuc))
            catId = await _db.Categories.Where(c => c.Slug == danhMuc).Select(c => (int?)c.Id).FirstOrDefaultAsync();

        var results = await _search.QueryAsync(new TopicQuery
        {
            Keyword = q, CategoryId = catId, TagSlug = the, Period = thoiGian, Sort = sort, Page = trang, PageSize = 15
        });

        SetSeo(new SeoModel
        {
            Title = string.IsNullOrWhiteSpace(q) ? "Tìm kiếm" : $"Tìm kiếm: {q}",
            Description = $"Kết quả tìm kiếm trên Diễn đàn Cửa cho từ khóa \"{q}\".",
            NoIndex = true // trang kết quả tìm kiếm không cần index
        });

        var vm = new TopicListViewModel
        {
            Topics = results, Keyword = q, Period = thoiGian, Sort = sort,
            AllCategories = await _db.Categories.OrderBy(c => c.DisplayOrder).ToListAsync(),
            Heading = string.IsNullOrWhiteSpace(q) ? "Tìm kiếm" : $"Kết quả cho “{q}”",
            BasePath = "/tim-kiem"
        };
        ViewData["DanhMuc"] = danhMuc;
        ViewData["The"] = the;
        ViewData["SortKey"] = sortKey;
        return View(vm);
    }

    [HttpGet("/tim-kiem/goi-y")]
    public async Task<IActionResult> Suggest(string? q)
    {
        var topics = await _search.AutocompleteAsync(q, 6);
        var tags = await _search.SuggestTagsAsync(q, 5);
        return Json(new
        {
            topics = topics.Select(t => new { t.Title, url = _url.Topic(t), category = t.Category?.Name }),
            tags = tags.Select(t => new { t.Name, t.Slug, t.UseCount })
        });
    }
}
