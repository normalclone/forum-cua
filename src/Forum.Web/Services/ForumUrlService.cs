using Microsoft.AspNetCore.Http;

namespace Forum.Web.Services;

/// <summary>
/// Sinh URL theo lược đồ SEO thống nhất của diễn đàn (slug thân thiện).
/// Tập trung hóa để canonical/sitemap/breadcrumb dùng chung một nguồn.
/// </summary>
public interface IForumUrlService
{
    string Topic(Topic topic);
    string Topic(int id, string slug);
    string Category(string slug, int page = 1, string? sort = null);
    string Tag(string slug, int page = 1);
    string User(string username);
    string Absolute(string relativePath);
    string BaseUrl { get; }
}

public class ForumUrlService : IForumUrlService
{
    private readonly IHttpContextAccessor _http;
    private readonly string _configuredBase;

    public ForumUrlService(IHttpContextAccessor http, IConfiguration config)
    {
        _http = http;
        _configuredBase = (config["SiteUrl"] ?? "https://localhost:5001").TrimEnd('/');
    }

    public string BaseUrl
    {
        get
        {
            var req = _http.HttpContext?.Request;
            if (req is not null)
                return $"{req.Scheme}://{req.Host}{req.PathBase}".TrimEnd('/');
            return _configuredBase;
        }
    }

    public string Topic(Topic topic) => Topic(topic.Id, topic.Slug);

    public string Topic(int id, string slug) =>
        $"/chu-de/{id}/{(string.IsNullOrEmpty(slug) ? "noi-dung" : slug)}";

    public string Category(string slug, int page = 1, string? sort = null)
    {
        var url = $"/danh-muc/{slug}";
        var query = new List<string>();
        if (page > 1) query.Add($"trang={page}");
        if (!string.IsNullOrEmpty(sort)) query.Add($"sap-xep={sort}");
        return query.Count > 0 ? $"{url}?{string.Join("&", query)}" : url;
    }

    public string Tag(string slug, int page = 1) =>
        page > 1 ? $"/the/{slug}?trang={page}" : $"/the/{slug}";

    public string User(string username) => $"/thanh-vien/{username}";

    public string Absolute(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return BaseUrl;
        if (relativePath.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return relativePath;
        return BaseUrl + (relativePath.StartsWith('/') ? relativePath : "/" + relativePath);
    }
}
