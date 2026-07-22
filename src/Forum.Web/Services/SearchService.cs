using Microsoft.EntityFrameworkCore;

namespace Forum.Web.Services;

/// <summary>Tham số truy vấn danh sách chủ đề (dùng cho trang chủ, danh mục, thẻ, tìm kiếm, hồ sơ).</summary>
public class TopicQuery
{
    public string? Keyword { get; set; }
    public int? CategoryId { get; set; }
    public string? CategorySlug { get; set; }
    public string? TagSlug { get; set; }
    public int? AuthorId { get; set; }
    public string? Period { get; set; }           // ngay | tuan | thang | nam | null(tất cả)
    public TopicSort Sort { get; set; } = TopicSort.Active;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public bool PinnedFirst { get; set; }          // ghim lên đầu (trang chủ/danh mục)
    public bool OnlyFeatured { get; set; }
    public bool ExcludeFeatured { get; set; }
    public int[]? ExcludeIds { get; set; }          // bỏ các chủ đề đã hiển thị nơi khác (vd strip nổi bật) khỏi feed
}

public interface ISearchService
{
    Task<PagedResult<Topic>> QueryAsync(TopicQuery q);
    Task<List<Topic>> FeaturedAsync(int take = 5, int? categoryId = null);
    Task<List<Topic>> AutocompleteAsync(string? keyword, int take = 6);
    Task<List<Tag>> SuggestTagsAsync(string? keyword, int take = 8);
}

public class SearchService : ISearchService
{
    private readonly ApplicationDbContext _db;

    public SearchService(ApplicationDbContext db) => _db = db;

    public async Task<PagedResult<Topic>> QueryAsync(TopicQuery q)
    {
        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 1, 100);

        IQueryable<Topic> query = _db.Topics
            .Include(t => t.Author)
            .Include(t => t.Category)
            .Include(t => t.TopicTags).ThenInclude(tt => tt.Tag)
            .Where(t => !t.IsDeleted && t.IsApproved);   // ẩn bài đang chờ kiểm duyệt khỏi danh sách công khai

        if (q.CategoryId is int cid)
            query = query.Where(t => t.CategoryId == cid);
        if (!string.IsNullOrEmpty(q.CategorySlug))
            query = query.Where(t => t.Category.Slug == q.CategorySlug);
        if (!string.IsNullOrEmpty(q.TagSlug))
            query = query.Where(t => t.TopicTags.Any(tt => tt.Tag.Slug == q.TagSlug));
        if (q.AuthorId is int aid)
            query = query.Where(t => t.AuthorId == aid);
        if (q.OnlyFeatured)
            query = query.Where(t => t.IsFeatured);
        if (q.ExcludeFeatured)
            query = query.Where(t => !t.IsFeatured);
        if (q.ExcludeIds is { Length: > 0 } ex)
            query = query.Where(t => !ex.Contains(t.Id));

        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            // LIKE '%kw%' có wildcard ĐẦU nên không dùng được index → full-scan cố ý.
            // Chấp nhận ở quy mô hiện tại (~vài chục–trăm chủ đề). Khi dữ liệu lớn
            // (hàng nghìn chủ đề hoặc latency rõ rệt) hãy chuyển sang full-text search:
            // SQLite FTS5 (virtual table) hoặc SQL Server full-text catalog — giữ sau ISearchService.
            var kw = $"%{q.Keyword.Trim()}%";
            query = query.Where(t => EF.Functions.Like(t.Title, kw) || EF.Functions.Like(t.Body, kw));
        }

        var cutoff = PeriodCutoff(q.Period);
        if (cutoff is DateTime since)
            query = query.Where(t => t.CreatedAt >= since);

        var total = await query.CountAsync();

        // Sắp xếp. Ghim ưu tiên nếu yêu cầu.
        IOrderedQueryable<Topic> ordered = q.PinnedFirst
            ? query.OrderByDescending(t => t.IsPinned)
            : query.OrderBy(t => 0);

        ordered = q.Sort switch
        {
            TopicSort.Latest => ordered.ThenByDescending(t => t.CreatedAt),
            TopicSort.Top => ordered.ThenByDescending(t => t.Score).ThenByDescending(t => t.CreatedAt),
            TopicSort.Trending => ordered.ThenByDescending(t => t.HotScore),
            _ => ordered.ThenByDescending(t => t.LastActivityAt)
        };

        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<Topic>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public Task<List<Topic>> FeaturedAsync(int take = 5, int? categoryId = null)
        => _db.Topics
            .Include(t => t.Author)
            .Include(t => t.Category)
            .Where(t => !t.IsDeleted && t.IsApproved && (t.IsFeatured || t.IsPinned))
            .Where(t => categoryId == null || t.CategoryId == categoryId)
            .OrderByDescending(t => t.IsPinned)
            .ThenByDescending(t => t.HotScore)
            .Take(take)
            .ToListAsync();

    public async Task<List<Topic>> AutocompleteAsync(string? keyword, int take = 6)
    {
        if (string.IsNullOrWhiteSpace(keyword) || keyword.Trim().Length < 2)
            return new List<Topic>();
        var kw = $"%{keyword.Trim()}%";
        return await _db.Topics
            .Include(t => t.Category)
            .Where(t => !t.IsDeleted && t.IsApproved && EF.Functions.Like(t.Title, kw))
            .OrderByDescending(t => t.Score)
            .ThenByDescending(t => t.LastActivityAt)
            .Take(take)
            .ToListAsync();
    }

    public async Task<List<Tag>> SuggestTagsAsync(string? keyword, int take = 8)
    {
        IQueryable<Tag> q = _db.Tags;
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = $"%{keyword.Trim()}%";
            q = q.Where(t => EF.Functions.Like(t.Name, kw) || EF.Functions.Like(t.Slug, kw));
        }
        return await q.OrderByDescending(t => t.UseCount).Take(take).ToListAsync();
    }

    private static DateTime? PeriodCutoff(string? period) => period switch
    {
        "ngay" => DateTime.UtcNow.AddDays(-1),
        "tuan" => DateTime.UtcNow.AddDays(-7),
        "thang" => DateTime.UtcNow.AddDays(-30),
        "nam" => DateTime.UtcNow.AddDays(-365),
        _ => null
    };
}
