namespace Forum.Web.Models.ViewModels;

/// <summary>Một mục trong thanh thông báo chạy (marquee). Text hiển thị, Url link (tuỳ chọn), Icon Lucide.</summary>
public record TickerItem(string Text, string? Url, string Icon);

public class HomeViewModel
{
    public List<Topic> Featured { get; set; } = new();
    public PagedResult<Topic> Feed { get; set; } = PagedResult<Topic>.Empty();
    public List<Category> Categories { get; set; } = new();
    public TopicSort Sort { get; set; } = TopicSort.Active;
    public string? Period { get; set; }
    public List<ApplicationUser> TopMembers { get; set; } = new();
    public int TotalTopics { get; set; }
    public int TotalMembers { get; set; }
    public int TotalComments { get; set; }
}

/// <summary>Dữ liệu cho danh sách chủ đề theo danh mục / thẻ / tìm kiếm.</summary>
public class TopicListViewModel
{
    public PagedResult<Topic> Topics { get; set; } = PagedResult<Topic>.Empty();
    public List<Topic> Featured { get; set; } = new();   // bài ghim/nổi bật của danh mục (strip đầu trang)
    public Category? Category { get; set; }
    public Tag? Tag { get; set; }
    public bool IsFollowingTag { get; set; }
    public string? Keyword { get; set; }
    public string? Period { get; set; }
    public TopicSort Sort { get; set; } = TopicSort.Active;
    public List<Category> AllCategories { get; set; } = new();
    public string Heading { get; set; } = "";
    public string BasePath { get; set; } = "/";
}
