namespace Forum.Web.Models;

/// <summary>Vote cho chủ đề. Value = +1 (up) hoặc -1 (down). Unique theo (UserId, TopicId).</summary>
public class TopicVote
{
    public int Id { get; set; }
    public int TopicId { get; set; }
    public Topic Topic { get; set; } = null!;
    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public short Value { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Vote cho bình luận. Value = +1 / -1. Unique theo (UserId, CommentId).</summary>
public class CommentVote
{
    public int Id { get; set; }
    public int CommentId { get; set; }
    public Comment Comment { get; set; } = null!;
    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public short Value { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Đánh dấu (bookmark / lưu) một chủ đề.</summary>
public class Bookmark
{
    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public int TopicId { get; set; }
    public Topic Topic { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

/// <summary>Theo dõi chủ đề để nhận thông báo khi có trả lời mới.</summary>
public class TopicSubscription
{
    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public int TopicId { get; set; }
    public Topic Topic { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

/// <summary>Thông báo gửi tới người dùng (trả lời, nhắc tên, vote, theo dõi, huy hiệu...).</summary>
public class Notification
{
    public int Id { get; set; }

    public int RecipientId { get; set; }
    public ApplicationUser Recipient { get; set; } = null!;

    public int? ActorId { get; set; }            // người gây ra thông báo (có thể null = hệ thống)
    public ApplicationUser? Actor { get; set; }

    public NotificationType Type { get; set; }

    public int? TopicId { get; set; }
    public Topic? Topic { get; set; }
    public int? CommentId { get; set; }
    public Comment? Comment { get; set; }

    public string? Message { get; set; }         // mô tả ngắn (tuỳ chọn, render sẵn)
    public string? Url { get; set; }             // liên kết đích
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
