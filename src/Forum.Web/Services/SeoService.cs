using System.Text.Json;

namespace Forum.Web.Services;

/// <summary>
/// Sinh dữ liệu có cấu trúc Schema.org JSON-LD và hỗ trợ dựng SeoModel.
/// DiscussionForumPosting cho chủ đề thảo luận, QAPage cho chủ đề dạng hỏi-đáp.
/// </summary>
public interface ISeoService
{
    string SiteName { get; }
    string DiscussionJsonLd(Topic topic, IReadOnlyCollection<Comment> topComments);
    string BreadcrumbJsonLd(IEnumerable<BreadcrumbItem> crumbs);
    string WebSiteJsonLd();
    string ProfileJsonLd(ApplicationUser user);
}

public class SeoService : ISeoService
{
    private readonly IForumUrlService _url;
    private readonly IMarkdownService _md;
    private readonly ISiteSettingService _settings;

    // Encoder mặc định escape < > & và ký tự non-ASCII -> an toàn khi nhúng trong <script>.
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public string SiteName => _settings.SiteName;

    public SeoService(IForumUrlService url, IMarkdownService md, ISiteSettingService settings)
    {
        _url = url;
        _md = md;
        _settings = settings;
    }

    public string WebSiteJsonLd()
    {
        var obj = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "WebSite",
            ["name"] = SiteName,
            ["url"] = _url.BaseUrl,
            ["potentialAction"] = new Dictionary<string, object?>
            {
                ["@type"] = "SearchAction",
                ["target"] = new Dictionary<string, object?>
                {
                    ["@type"] = "EntryPoint",
                    ["urlTemplate"] = _url.Absolute("/tim-kiem?q={search_term_string}")
                },
                ["query-input"] = "required name=search_term_string"
            }
        };
        return Serialize(obj);
    }

    public string DiscussionJsonLd(Topic topic, IReadOnlyCollection<Comment> topComments)
    {
        var authorObj = Person(topic.Author);
        var url = _url.Absolute(_url.Topic(topic));

        var comments = topComments.Where(c => !c.IsDeleted).Select(c => new Dictionary<string, object?>
        {
            ["@type"] = "Comment",
            ["text"] = _md.Excerpt(c.Body, 500),
            ["dateCreated"] = c.CreatedAt.ToString("o"),
            ["author"] = Person(c.Author),
            ["upvoteCount"] = c.UpvoteCount
        }).Cast<object?>().ToList();

        var obj = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = topic.IsQuestion ? "QAPage" : "DiscussionForumPosting",
            ["headline"] = topic.Title,
            ["name"] = topic.Title,
            ["text"] = _md.Excerpt(topic.Body, 500),
            ["url"] = url,
            ["mainEntityOfPage"] = url,
            ["datePublished"] = topic.CreatedAt.ToString("o"),
            ["dateModified"] = (topic.UpdatedAt ?? topic.LastActivityAt).ToString("o"),
            ["author"] = authorObj,
            ["publisher"] = new Dictionary<string, object?>
            {
                ["@type"] = "Organization",
                ["name"] = SiteName,
                ["url"] = _url.BaseUrl
            },
            ["interactionStatistic"] = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    ["@type"] = "InteractionCounter",
                    ["interactionType"] = "https://schema.org/CommentAction",
                    ["userInteractionCount"] = topic.CommentCount
                },
                new Dictionary<string, object?>
                {
                    ["@type"] = "InteractionCounter",
                    ["interactionType"] = "https://schema.org/LikeAction",
                    ["userInteractionCount"] = topic.UpvoteCount
                },
                new Dictionary<string, object?>
                {
                    ["@type"] = "InteractionCounter",
                    ["interactionType"] = "https://schema.org/ViewAction",
                    ["userInteractionCount"] = topic.ViewCount
                }
            }
        };

        if (comments.Count > 0)
            obj["comment"] = comments;

        return Serialize(obj);
    }

    public string BreadcrumbJsonLd(IEnumerable<BreadcrumbItem> crumbs)
    {
        var items = new List<object?>();
        var pos = 1;
        foreach (var c in crumbs)
        {
            var item = new Dictionary<string, object?>
            {
                ["@type"] = "ListItem",
                ["position"] = pos++,
                ["name"] = c.Name
            };
            if (!string.IsNullOrEmpty(c.Url))
                item["item"] = _url.Absolute(c.Url);
            items.Add(item);
        }

        var obj = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "BreadcrumbList",
            ["itemListElement"] = items
        };
        return Serialize(obj);
    }

    public string ProfileJsonLd(ApplicationUser user)
    {
        var obj = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "ProfilePage",
            ["mainEntity"] = Person(user)
        };
        return Serialize(obj);
    }

    private Dictionary<string, object?> Person(ApplicationUser? user)
    {
        if (user is null)
            return new Dictionary<string, object?> { ["@type"] = "Person", ["name"] = "Ẩn danh" };

        return new Dictionary<string, object?>
        {
            ["@type"] = "Person",
            ["name"] = string.IsNullOrEmpty(user.DisplayName) ? user.UserName : user.DisplayName,
            ["url"] = _url.Absolute(_url.User(user.UserName ?? user.Id.ToString()))
        };
    }

    private static string Serialize(object obj) => JsonSerializer.Serialize(obj, JsonOpts);
}
