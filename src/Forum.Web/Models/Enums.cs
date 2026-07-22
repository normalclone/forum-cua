namespace Forum.Web.Models;

/// <summary>Vai trò người dùng trong ngành cửa (dùng cho hồ sơ &amp; hover card).</summary>
public enum UserTrade
{
    ChuNha = 0,      // Chủ nhà đang tìm cửa
    ThoLapDat = 1,   // Thợ lắp đặt
    KienTrucSu = 2,  // Kiến trúc sư
    DaiLy = 3,       // Đại lý / nhà phân phối
    KySuVatLieu = 4, // Kỹ sư vật liệu
    Khac = 5
}

public enum NotificationType
{
    Reply = 0,      // Có người trả lời chủ đề/bình luận của bạn
    Mention = 1,    // Được nhắc tên @username
    Vote = 2,       // Bài/bình luận của bạn được vote
    Follow = 3,     // Có người theo dõi bạn
    Badge = 4,      // Nhận huy hiệu mới
    Message = 5,    // Tin nhắn mới
    Moderation = 6, // Hành động kiểm duyệt liên quan tới bạn
    TagTopic = 7    // Chủ đề mới thuộc thẻ bạn theo dõi
}

/// <summary>Đối tượng của vote/report/moderation: chủ đề hoặc bình luận.</summary>
public enum ContentTargetType
{
    Topic = 0,
    Comment = 1
}

public enum ReportStatus
{
    Pending = 0,
    Resolved = 1,
    Dismissed = 2
}

public enum ModerationAction
{
    Pin = 0,
    Unpin = 1,
    Lock = 2,
    Unlock = 3,
    Move = 4,
    Delete = 5,
    Restore = 6,
    ResolveReport = 7,
    DismissReport = 8,
    EditContent = 9,
    Feature = 10,
    Unfeature = 11,
    Approve = 12,
    Reject = 13,
    Warn = 14,
    Mute = 15,
    Unmute = 16
}

public enum BadgeTier
{
    Bronze = 0,
    Silver = 1,
    Gold = 2
}

/// <summary>Kiểu sắp xếp danh sách chủ đề.</summary>
public enum TopicSort
{
    Latest = 0,   // Mới nhất
    Top = 1,      // Nổi bật (điểm cao)
    Trending = 2, // Xu hướng (hot score)
    Active = 3    // Hoạt động gần nhất
}

public enum ActivityType
{
    CreatedTopic = 0,
    PostedComment = 1,
    ReceivedUpvote = 2,
    EarnedBadge = 3,
    Bookmarked = 4
}

/// <summary>Tên vai trò chuẩn dùng cho ASP.NET Core Identity.</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Moderator = "Moderator";
    public const string Member = "Member";

    public static readonly string[] All = { Admin, Moderator, Member };
    public const string Staff = Admin + "," + Moderator; // dùng cho [Authorize(Roles = Roles.Staff)]
}
