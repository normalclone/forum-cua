namespace Forum.Web.Models;

/// <summary>Cấu hình site chỉnh sửa lúc chạy (key-value). Quản trị viên sửa ở /quan-tri/cau-hinh.</summary>
public class SiteSetting
{
    public string Key { get; set; } = string.Empty;   // khóa chính
    public string Value { get; set; } = string.Empty;
}

/// <summary>Các khóa cấu hình đã biết.</summary>
public static class SettingKeys
{
    public const string SiteName = "site.name";
    public const string SiteDescription = "site.description";
    public const string BannedWords = "filter.banned";   // ngăn cách bằng dấu phẩy hoặc xuống dòng

    // Feature flags (bật/tắt tính năng toàn site)
    public const string FeatureRegistration = "feature.registration";
    public const string FeaturePosting = "feature.posting";
    public const string FeatureChat = "feature.chat";
    public const string FeaturePolls = "feature.polls";

    // Thương hiệu / giao diện
    public const string BrandAccent = "brand.accent";    // màu nhấn (hex)
    public const string BrandLogo = "brand.logo";        // URL logo (/uploads/..)
    public const string BrandFavicon = "brand.favicon";  // URL favicon

    // Tự động kiểm duyệt
    public const string AutomodNewUser = "automod.newUser";        // bài của tài khoản mới → chờ duyệt
    public const string AutomodNewUserDays = "automod.newUserDays"; // "mới" = tạo dưới N ngày
    public const string AutomodSpam = "automod.spam";              // bài nghi spam → chờ duyệt

    // Chống spam: giới hạn tần suất (rate limit). 0 = tắt.
    public const string RateTopicsPerHour = "rate.topicsPerHour";      // số chủ đề tối đa / giờ / người
    public const string RateCommentsPerMinute = "rate.commentsPerMinute"; // số bình luận tối đa / phút / người

    // Lý do kiểm duyệt mẫu (mỗi dòng một lý do) — dùng cho từ chối/khóa/cảnh cáo.
    public const string CannedReasons = "mod.cannedReasons";
}
