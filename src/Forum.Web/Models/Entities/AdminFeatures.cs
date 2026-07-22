namespace Forum.Web.Models;

/// <summary>Thông báo chạy (marquee) do quản trị soạn — quản lý ở /quan-tri/thong-bao.</summary>
public class Announcement
{
    public int Id { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Url { get; set; }                 // link tuỳ chọn khi bấm vào
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    public DateTime? StartsAt { get; set; }          // hẹn giờ bắt đầu (UTC), null = ngay
    public DateTime? EndsAt { get; set; }            // hết hạn (UTC), null = vô hạn
    public DateTime CreatedAt { get; set; }
}

/// <summary>Lịch sử cảnh cáo gửi tới người dùng (kèm khoá tạm nếu có).</summary>
public class UserWarning
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public int ModeratorId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>Trang tĩnh (CMS): Nội quy, Giới thiệu… sửa được ở /quan-tri/trang.</summary>
public class CmsPage
{
    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;   // Markdown
    public bool IsPublished { get; set; } = true;
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Phân công kiểm duyệt viên phụ trách một danh mục.</summary>
public class CategoryModerator
{
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
}
