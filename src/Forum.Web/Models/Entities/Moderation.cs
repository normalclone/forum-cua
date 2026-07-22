namespace Forum.Web.Models;

/// <summary>Báo cáo / gắn cờ nội dung không phù hợp.</summary>
public class Report
{
    public int Id { get; set; }

    public int ReporterId { get; set; }
    public ApplicationUser Reporter { get; set; } = null!;

    public ContentTargetType TargetType { get; set; }
    public int? TopicId { get; set; }
    public Topic? Topic { get; set; }
    public int? CommentId { get; set; }
    public Comment? Comment { get; set; }

    public string Reason { get; set; } = string.Empty;
    public string? Details { get; set; }

    public ReportStatus Status { get; set; } = ReportStatus.Pending;
    public int? ResolvedById { get; set; }
    public ApplicationUser? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Nhật ký kiểm duyệt (audit log) — ai làm gì, khi nào.</summary>
public class ModerationLog
{
    public int Id { get; set; }

    public int ModeratorId { get; set; }
    public ApplicationUser Moderator { get; set; } = null!;

    public ModerationAction Action { get; set; }
    public ContentTargetType TargetType { get; set; }

    /// <summary>
    /// Khóa đối tượng bị tác động, phân giải theo <see cref="TargetType"/> (Topic/Comment...).
    /// CỐ Ý không đặt FK: một cột không thể tham chiếu nhiều bảng, và log kiểm duyệt
    /// phải tồn tại kể cả sau khi đối tượng bị xóa (audit). Tiêu đề được chụp ở <see cref="TargetTitle"/>.
    /// </summary>
    public int TargetId { get; set; }
    public string? TargetTitle { get; set; }   // chụp nhanh tiêu đề/đoạn nội dung để log dễ đọc
    public string? Detail { get; set; }         // lý do / ghi chú
    public DateTime CreatedAt { get; set; }
}
