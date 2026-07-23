namespace Forum.Web.Models;

/// <summary>Danh mục / box thảo luận (vd: Cửa gỗ, Cửa nhôm kính...).</summary>
public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string IconName { get; set; } = "door-open"; // tên icon Lucide
    public string ColorHex { get; set; } = "#4f8cff";
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }

    // Phân cấp (tuỳ chọn) — seed dạng phẳng nhưng schema hỗ trợ box con.
    public int? ParentCategoryId { get; set; }
    public Category? ParentCategory { get; set; }
    public ICollection<Category> Children { get; set; } = new List<Category>();

    /// <summary>Nếu bật: chủ đề đăng trong danh mục này phải được kiểm duyệt trước khi hiển thị.</summary>
    public bool RequireApproval { get; set; }

    /// <summary>Vai trò tối thiểu để XEM danh mục: "" = ai cũng xem; "Member" = phải đăng nhập;
    /// "Moderator"/"Admin" = danh mục riêng tư của nhân sự.</summary>
    public string MinRoleToView { get; set; } = "";

    public ICollection<Topic> Topics { get; set; } = new List<Topic>();
}

/// <summary>Chủ đề thảo luận (thread). Body lưu Markdown thô; render khi hiển thị.</summary>
public class Topic
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public int AuthorId { get; set; }
    public ApplicationUser Author { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }          // có giá trị => hiển thị "đã chỉnh sửa"
    public DateTime LastActivityAt { get; set; }

    public int ViewCount { get; set; }
    public bool IsPinned { get; set; }
    public bool IsLocked { get; set; }
    public bool IsFeatured { get; set; }              // nổi bật trên board chung
    public bool IsDeleted { get; set; }               // soft delete
    public bool IsApproved { get; set; } = true;      // false = chờ kiểm duyệt (danh mục yêu cầu duyệt)
    public bool IsQuestion { get; set; }              // QAPage vs DiscussionForumPosting (JSON-LD)

    /// <summary>Bình luận được chọn làm "đáp án được chấp nhận" (chỉ dùng cho chủ đề Hỏi–Đáp).
    /// CỐ Ý pseudo-FK loose (không nav/FK) để tránh chu trình FK Topic↔Comment; phân giải qua truy vấn.</summary>
    public int? AcceptedAnswerId { get; set; }

    // Số liệu denormalized để sắp xếp/hiển thị nhanh.
    public int Score { get; set; }                    // Upvote - Downvote
    public int UpvoteCount { get; set; }
    public int DownvoteCount { get; set; }
    public int CommentCount { get; set; }
    public double HotScore { get; set; }              // dùng để sắp xếp "xu hướng"

    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<TopicTag> TopicTags { get; set; } = new List<TopicTag>();
    public ICollection<TopicVote> Votes { get; set; } = new List<TopicVote>();
    public ICollection<Bookmark> Bookmarks { get; set; } = new List<Bookmark>();
    public ICollection<TopicSubscription> Subscriptions { get; set; } = new List<TopicSubscription>();
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
    public Poll? Poll { get; set; }
}

/// <summary>
/// Bình luận / trả lời, hỗ trợ lồng nhau qua ParentCommentId.
/// Dùng materialized path (Path) + Depth để sắp xếp &amp; render cây bình luận hiệu quả,
/// trung lập provider (không dùng HierarchyId đặc thù SQL Server).
/// </summary>
public class Comment
{
    public int Id { get; set; }

    public int TopicId { get; set; }
    public Topic Topic { get; set; } = null!;

    public int AuthorId { get; set; }
    public ApplicationUser Author { get; set; } = null!;

    public int? ParentCommentId { get; set; }
    public Comment? ParentComment { get; set; }
    public ICollection<Comment> Replies { get; set; } = new List<Comment>();

    public string Body { get; set; } = string.Empty;

    /// <summary>Đường dẫn vật chất, vd "0000001/0000005" — id zero-padded nối bằng "/".</summary>
    public string Path { get; set; } = string.Empty;
    public int Depth { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public int Score { get; set; }
    public int UpvoteCount { get; set; }
    public int DownvoteCount { get; set; }

    public ICollection<CommentVote> Votes { get; set; } = new List<CommentVote>();
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
}

/// <summary>Thẻ (tag) gắn cho chủ đề.</summary>
public class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int UseCount { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<TopicTag> TopicTags { get; set; } = new List<TopicTag>();
}

/// <summary>Bảng nối nhiều-nhiều giữa Topic và Tag.</summary>
public class TopicTag
{
    public int TopicId { get; set; }
    public Topic Topic { get; set; } = null!;

    public int TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}
