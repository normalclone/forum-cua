namespace Forum.Web.Models;

/// <summary>File / hình ảnh đính kèm cho chủ đề hoặc bình luận (lưu dưới wwwroot/uploads).</summary>
public class Attachment
{
    public int Id { get; set; }

    public int UploaderId { get; set; }
    public ApplicationUser Uploader { get; set; } = null!;

    public int? TopicId { get; set; }
    public Topic? Topic { get; set; }
    public int? CommentId { get; set; }
    public Comment? Comment { get; set; }

    public string FileName { get; set; } = string.Empty;     // tên gốc
    public string StoredPath { get; set; } = string.Empty;   // đường dẫn tương đối từ wwwroot (vd /uploads/xxx.jpg)
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public bool IsImage { get; set; }
    public string? AltText { get; set; }                     // alt cho SEO/accessibility
    public DateTime CreatedAt { get; set; }
}
