namespace Forum.Web.Models.ViewModels;

/// <summary>Trang tổng quan quản trị.</summary>
public class AdminDashboardViewModel
{
    public int UserCount { get; set; }
    public int LockedUserCount { get; set; }
    public int TopicCount { get; set; }
    public int CommentCount { get; set; }
    public int CategoryCount { get; set; }
    public int TagCount { get; set; }
    public int PendingReportCount { get; set; }
    public int TodayTopics { get; set; }
    public int TodayComments { get; set; }
    public List<(string Name, string Color, int Count)> TopCategories { get; set; } = new();
    public List<ApplicationUser> NewestUsers { get; set; } = new();
    public List<ActivityPoint> Activity { get; set; } = new();   // hoạt động theo ngày (30 ngày)
}

/// <summary>Một ngày trong biểu đồ hoạt động.</summary>
public record ActivityPoint(DateTime Day, int Topics, int Comments, int Users);

/// <summary>Một dòng người dùng trong bảng quản trị.</summary>
public class AdminUserRow
{
    public ApplicationUser User { get; set; } = null!;
    public string Role { get; set; } = Roles.Member;
    public bool IsLocked { get; set; }
    public int TopicCount { get; set; }
    public int CommentCount { get; set; }
}

public class AdminUsersViewModel
{
    public PagedResult<AdminUserRow> Rows { get; set; } = PagedResult<AdminUserRow>.Empty();
    public string? Keyword { get; set; }
    public string? RoleFilter { get; set; }
    public int CurrentUserId { get; set; }
}

public class AdminUserDetailViewModel
{
    public ApplicationUser User { get; set; } = null!;
    public string Role { get; set; } = Roles.Member;
    public bool IsLocked { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public int TopicCount { get; set; }
    public int CommentCount { get; set; }
    public List<Topic> RecentTopics { get; set; } = new();
    public List<Comment> RecentComments { get; set; } = new();
    public List<UserWarning> Warnings { get; set; } = new();
    public bool IsSelf { get; set; }
}

/// <summary>Quản lý danh mục.</summary>
public class AdminCategoriesViewModel
{
    public List<Category> Categories { get; set; } = new();
    public Dictionary<int, int> TopicCounts { get; set; } = new();
    public Dictionary<int, List<string>> Moderators { get; set; } = new();   // categoryId → username kiểm duyệt viên
}

/// <summary>Dữ liệu form thêm/sửa danh mục.</summary>
public class CategoryEditModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string IconName { get; set; } = "door-open";
    public string ColorHex { get; set; } = "#4f8cff";
    public int DisplayOrder { get; set; }
    public bool RequireApproval { get; set; }
    public string? Moderators { get; set; }   // username kiểm duyệt viên, cách nhau dấu phẩy
    public string? MinRoleToView { get; set; } // "" công khai; "Member"/"Moderator"/"Admin"
}

public class AdminTagsViewModel
{
    public PagedResult<Tag> Tags { get; set; } = PagedResult<Tag>.Empty();
    public string? Keyword { get; set; }
}

public class AdminSettingsViewModel
{
    public string SiteName { get; set; } = string.Empty;
    public string SiteDescription { get; set; } = string.Empty;
    public string BannedWords { get; set; } = string.Empty;

    // Feature flags
    public bool FeatureRegistration { get; set; } = true;
    public bool FeaturePosting { get; set; } = true;
    public bool FeatureChat { get; set; } = true;
    public bool FeaturePolls { get; set; } = true;

    // Thương hiệu
    public string? BrandAccent { get; set; }
    public string? BrandLogo { get; set; }
    public string? BrandFavicon { get; set; }

    // Auto-moderation
    public bool AutomodNewUser { get; set; }
    public int AutomodNewUserDays { get; set; } = 3;
    public bool AutomodSpam { get; set; } = true;

    // Chống spam (rate limit) — 0 = tắt
    public int RateTopicsPerHour { get; set; }
    public int RateCommentsPerMinute { get; set; }

    // Lý do kiểm duyệt mẫu (mỗi dòng một lý do)
    public string CannedReasons { get; set; } = string.Empty;
}

public record AnalyticsTopic(string Title, string Url, int Views, int Score);
public record AnalyticsUser(string Name, string Username, int Reputation);

public class AdminAnalyticsViewModel
{
    public int NewTopics30 { get; set; }
    public int NewComments30 { get; set; }
    public int NewUsers30 { get; set; }
    public int ActiveUsers30 { get; set; }
    public int ApprovedTopics { get; set; }
    public int PendingTopics { get; set; }
    public List<AnalyticsTopic> TopTopics { get; set; } = new();
    public List<AnalyticsUser> TopUsers { get; set; } = new();
    public int[] TopicsByHour { get; set; } = new int[24];   // theo giờ VN (UTC+7)
}

// ---------------- Trang tĩnh (CMS) ----------------
public class AdminPagesViewModel
{
    public List<CmsPage> Pages { get; set; } = new();
}

public class CmsPageEditModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsPublished { get; set; } = true;
}

// ---------------- Thông báo chạy ----------------
public class AdminAnnouncementsViewModel
{
    public List<Announcement> Items { get; set; } = new();
}

public class AnnouncementEditModel
{
    public int Id { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Url { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
}

// ---------------- Quản lý nội dung ----------------
public class AdminContentViewModel
{
    public PagedResult<Topic> Topics { get; set; } = PagedResult<Topic>.Empty();
    public List<Category> Categories { get; set; } = new();
    public string? Keyword { get; set; }
    public string? CategorySlug { get; set; }
    public bool IncludeDeleted { get; set; }
}

public class AdminCommentsViewModel
{
    public PagedResult<Comment> Comments { get; set; } = PagedResult<Comment>.Empty();
    public string? Keyword { get; set; }
    public bool IncludeDeleted { get; set; }
}

// ---------------- Quản lý huy hiệu ----------------
public class AdminBadgeRow
{
    public Badge Badge { get; set; } = null!;
    public int Holders { get; set; }
}

public class AdminBadgesViewModel
{
    public List<AdminBadgeRow> Badges { get; set; } = new();
}

public class BadgeEditModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconName { get; set; } = "award";
    public string ColorHex { get; set; } = "#c9a227";
    public BadgeTier Tier { get; set; } = BadgeTier.Bronze;
}
