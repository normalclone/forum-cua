namespace Forum.Web.Models;

/// <summary>Thả cảm xúc (emoji) trên chủ đề HOẶC bình luận — polymorphic loose.
/// Một người có thể thả nhiều loại emoji khác nhau nhưng mỗi (user, target, emoji) là duy nhất.</summary>
public class Reaction
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public int? TopicId { get; set; }
    public Topic? Topic { get; set; }

    public int? CommentId { get; set; }
    public Comment? Comment { get; set; }

    public string Emoji { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>Theo dõi một thẻ: bảng tin lọc theo thẻ + thông báo khi có chủ đề mới gắn thẻ đó.</summary>
public class TagSubscription
{
    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public int TagId { get; set; }
    public Tag Tag { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}

/// <summary>Ghi chú nội bộ về một thành viên — chỉ điều hành viên/quản trị viên xem được.</summary>
public class UserNote
{
    public int Id { get; set; }

    /// <summary>Thành viên được ghi chú.</summary>
    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    /// <summary>Nhân sự đã viết ghi chú.</summary>
    public int AuthorId { get; set; }
    public ApplicationUser Author { get; set; } = null!;

    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
