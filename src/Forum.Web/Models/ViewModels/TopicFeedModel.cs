namespace Forum.Web.Models.ViewModels;

/// <summary>
/// Phần "danh sách bài viết" của feed: topic-list + pager, hoặc trạng thái rỗng.
/// Dùng chung cho trang chủ và trang danh mục/thẻ — render được cả full-page lẫn AJAX
/// (xem <c>Views/Shared/_TopicFeed.cshtml</c> và <c>wwwroot/js/feed.js</c>).
/// </summary>
public class TopicFeedModel
{
    public PagedResult<Topic> Items { get; set; } = PagedResult<Topic>.Empty();

    /// <summary>Đường dẫn gốc cho link phân trang (vd "/", "/danh-muc/cua-go").</summary>
    public string BasePath { get; set; } = "/";

    /// <summary>Query giữ lại khi phân trang (vd "sap-xep=moi"), rỗng nếu mặc định.</summary>
    public string PagerQuery { get; set; } = "";

    // Trạng thái rỗng tùy ngữ cảnh.
    public string EmptyIcon { get; set; } = "door-open";
    public string EmptyTitle { get; set; } = "Chưa có chủ đề nào";
    public string EmptyText { get; set; } = "Hãy là người đầu tiên mở màn thảo luận!";
    public string EmptyCta { get; set; } = "Tạo chủ đề";
    public string EmptyCtaHref { get; set; } = "/tao-chu-de";
}
