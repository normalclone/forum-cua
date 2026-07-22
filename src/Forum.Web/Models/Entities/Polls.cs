namespace Forum.Web.Models;

/// <summary>Bình chọn (poll) gắn vào một chủ đề (quan hệ 1-1, tuỳ chọn).</summary>
public class Poll
{
    public int Id { get; set; }
    public int TopicId { get; set; }
    public Topic Topic { get; set; } = null!;

    public string Question { get; set; } = string.Empty;
    public bool AllowMultiple { get; set; }
    public DateTime? ClosesAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<PollOption> Options { get; set; } = new List<PollOption>();
}

public class PollOption
{
    public int Id { get; set; }
    public int PollId { get; set; }
    public Poll Poll { get; set; } = null!;

    public string Text { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public int VoteCount { get; set; }   // denormalized

    public ICollection<PollVote> Votes { get; set; } = new List<PollVote>();
}

public class PollVote
{
    public int Id { get; set; }
    public int PollId { get; set; }       // giữ để kiểm tra "1 vote / poll / user"
    public int PollOptionId { get; set; }
    public PollOption PollOption { get; set; } = null!;
    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
