using System.ComponentModel.DataAnnotations;

namespace Forum.Web.Models.ViewModels;

public class TopicDetailViewModel
{
    public Topic Topic { get; set; } = null!;
    public List<CommentNode> CommentRoots { get; set; } = new();
    public int CommentCount { get; set; }
    public int UserTopicVote { get; set; }
    public Dictionary<int, int> UserCommentVotes { get; set; } = new();
    public bool IsBookmarked { get; set; }
    public bool IsSubscribed { get; set; }
    public int? UserPollOptionId { get; set; }
    public int PollTotalVotes { get; set; }
    public bool CanEdit { get; set; }
    public bool CanModerate { get; set; }
    public bool CanAcceptAnswer { get; set; }     // tác giả chủ đề hoặc nhân sự → chọn đáp án (Hỏi–Đáp)
    public bool IsPendingApproval { get; set; }   // bài đang chờ duyệt (chỉ tác giả/staff thấy)
    public string CommentSort { get; set; } = "top";

    // Cảm xúc (emoji)
    public Forum.Web.Services.ReactionSummary TopicReactions { get; set; } = Forum.Web.Services.ReactionSummary.Empty;
    public Dictionary<int, Forum.Web.Services.ReactionSummary> CommentReactions { get; set; } = new();
    public IReadOnlyList<string> AllowedEmojis { get; set; } = System.Array.Empty<string>();
}

public class CommentNode
{
    public Comment Comment { get; set; } = null!;
    public List<CommentNode> Children { get; set; } = new();
}

/// <summary>Model truyền cho partial _Comment (đệ quy).</summary>
/// <summary>Thanh cảm xúc (emoji) tái sử dụng cho chủ đề &amp; bình luận.</summary>
public class ReactionBarModel
{
    public bool IsComment { get; set; }
    public int Id { get; set; }
    public Forum.Web.Services.ReactionSummary Summary { get; set; } = Forum.Web.Services.ReactionSummary.Empty;
    public IReadOnlyList<string> Allowed { get; set; } = System.Array.Empty<string>();
    public bool CanReact { get; set; }
}

public class CommentRenderModel
{
    public CommentNode Node { get; set; } = null!;
    public Dictionary<int, int> UserVotes { get; set; } = new();
    public int CurrentUserId { get; set; }
    public bool CanModerate { get; set; }
    public bool TopicLocked { get; set; }

    // Tương tác bổ sung
    public int TopicId { get; set; }
    public bool IsQuestion { get; set; }
    public int? AcceptedAnswerId { get; set; }
    public bool CanAccept { get; set; }   // tác giả chủ đề hoặc nhân sự → được chọn đáp án
    public Dictionary<int, Forum.Web.Services.ReactionSummary> Reactions { get; set; } = new();
    public IReadOnlyList<string> AllowedEmojis { get; set; } = System.Array.Empty<string>();

    public CommentRenderModel ForChild(CommentNode child) => new()
    {
        Node = child, UserVotes = UserVotes, CurrentUserId = CurrentUserId,
        CanModerate = CanModerate, TopicLocked = TopicLocked,
        TopicId = TopicId, IsQuestion = IsQuestion, AcceptedAnswerId = AcceptedAnswerId,
        CanAccept = CanAccept, Reactions = Reactions, AllowedEmojis = AllowedEmojis
    };
}

public class CreateTopicViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tiêu đề")]
    [StringLength(300, MinimumLength = 10, ErrorMessage = "Tiêu đề 10–300 ký tự")]
    [Display(Name = "Tiêu đề")]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng chọn danh mục")]
    [Display(Name = "Danh mục")]
    public int CategoryId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập nội dung")]
    [MinLength(20, ErrorMessage = "Nội dung tối thiểu 20 ký tự")]
    [Display(Name = "Nội dung")]
    public string Body { get; set; } = "";

    [Display(Name = "Thẻ")]
    public string? Tags { get; set; }

    [Display(Name = "Đây là câu hỏi (hỏi-đáp)")]
    public bool IsQuestion { get; set; }

    [Display(Name = "Thêm bình chọn")]
    public bool AddPoll { get; set; }
    public string? PollQuestion { get; set; }
    public string? PollOptionsText { get; set; }

    public int? DraftId { get; set; }
    public List<Category> Categories { get; set; } = new();
}

public class EditTopicViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề")]
    [StringLength(300, MinimumLength = 10, ErrorMessage = "Tiêu đề 10–300 ký tự")]
    [Display(Name = "Tiêu đề")]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập nội dung")]
    [MinLength(20, ErrorMessage = "Nội dung tối thiểu 20 ký tự")]
    [Display(Name = "Nội dung")]
    public string Body { get; set; } = "";

    [Display(Name = "Thẻ")]
    public string? Tags { get; set; }
}
