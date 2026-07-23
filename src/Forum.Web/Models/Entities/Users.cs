using Microsoft.AspNetCore.Identity;

namespace Forum.Web.Models;

/// <summary>
/// Người dùng diễn đàn. Mở rộng IdentityUser với khóa kiểu int để URL/sitemap gọn
/// và giữ trung lập provider (dễ chuyển SQLite -&gt; SQL Server).
/// </summary>
public class ApplicationUser : IdentityUser<int>
{
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Location { get; set; }

    /// <summary>Vai trò trong ngành cửa (hiển thị trên hồ sơ &amp; hover card).</summary>
    public UserTrade Trade { get; set; } = UserTrade.Khac;

    /// <summary>Điểm uy tín (denormalized, cập nhật qua ReputationService).</summary>
    public int Reputation { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime LastActiveAt { get; set; }

    /// <summary>Tạm cấm đăng bài/bình luận đến thời điểm này (mute) — nhẹ hơn khóa tài khoản.
    /// null hoặc quá khứ = không bị cấm nói.</summary>
    public DateTimeOffset? MutedUntil { get; set; }

    // Tùy chọn nhận thông báo (mặc định bật).
    public bool NotifyReplies { get; set; } = true;
    public bool NotifyMentions { get; set; } = true;
    public bool NotifyFollows { get; set; } = true;
    public bool NotifyTagTopics { get; set; } = true;

    // Navigation
    public ICollection<Topic> Topics { get; set; } = new List<Topic>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<UserBadge> UserBadges { get; set; } = new List<UserBadge>();
    public ICollection<UserFollow> Following { get; set; } = new List<UserFollow>();  // mình theo dõi
    public ICollection<UserFollow> Followers { get; set; } = new List<UserFollow>();  // theo dõi mình
}

public class ApplicationRole : IdentityRole<int>
{
    public string? Description { get; set; }
}

/// <summary>Quan hệ "theo dõi người dùng" (cho hover card &amp; feed).</summary>
public class UserFollow
{
    public int FollowerId { get; set; }
    public ApplicationUser Follower { get; set; } = null!;

    public int FolloweeId { get; set; }
    public ApplicationUser Followee { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}

/// <summary>Huy hiệu trao cho hoạt động (đăng bài, được vote, uy tín...).</summary>
public class Badge
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconName { get; set; } = "award";
    public string ColorHex { get; set; } = "#c9a227";
    public BadgeTier Tier { get; set; } = BadgeTier.Bronze;

    public ICollection<UserBadge> UserBadges { get; set; } = new List<UserBadge>();
}

public class UserBadge
{
    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public int BadgeId { get; set; }
    public Badge Badge { get; set; } = null!;

    public DateTime AwardedAt { get; set; }
}

/// <summary>Mục lịch sử hoạt động (dùng cho feed cá nhân &amp; tab hồ sơ).</summary>
public class UserActivity
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public ActivityType Type { get; set; }
    public int? TopicId { get; set; }
    public Topic? Topic { get; set; }

    /// <summary>
    /// Bình luận liên quan (tùy loại hoạt động). CỐ Ý là pseudo-FK loose (không nav/FK):
    /// bản ghi hoạt động giữ lại kể cả sau khi bình luận bị xóa — khác <see cref="TopicId"/>
    /// vốn có nav <see cref="Topic"/> + FK thật.
    /// </summary>
    public int? CommentId { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Bản nháp chủ đề tự động lưu khi đang soạn (server-side autosave).</summary>
public class Draft
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    /// <summary>
    /// Danh mục dự kiến của bản nháp. CỐ Ý là pseudo-FK loose (không đặt nav/FK/migration):
    /// nháp giữ được id danh mục kể cả khi danh mục thay đổi, không ràng buộc tham chiếu.
    /// </summary>
    public int? CategoryId { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public string? TagsCsv { get; set; }
    public DateTime UpdatedAt { get; set; }
}
