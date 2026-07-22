namespace Forum.Web.Models.ViewModels;

public class ProfileViewModel
{
    public ApplicationUser User { get; set; } = null!;
    public IList<string> Roles { get; set; } = new List<string>();
    public int TopicCount { get; set; }
    public int CommentCount { get; set; }
    public int FollowerCount { get; set; }
    public int FollowingCount { get; set; }
    public bool IsFollowing { get; set; }
    public bool IsSelf { get; set; }
    public string Tab { get; set; } = "chu-de";
    public PagedResult<Topic> Topics { get; set; } = PagedResult<Topic>.Empty();
    public List<Comment> Comments { get; set; } = new();
    public List<UserBadge> Badges { get; set; } = new();
    public List<UserNote> Notes { get; set; } = new();   // ghi chú nội bộ (chỉ nhân sự thấy)
    public bool TargetIsStaff => Roles.Contains("Admin") || Roles.Contains("Moderator");

    public string PrimaryRole =>
        Roles.Contains(Roles2.Admin) ? Roles2.Admin :
        Roles.Contains(Roles2.Moderator) ? Roles2.Moderator : Roles2.Member;
}

public class HoverCardViewModel
{
    public ApplicationUser User { get; set; } = null!;
    public string PrimaryRole { get; set; } = "Member";
    public int BadgeCount { get; set; }
    public int TopicCount { get; set; }
    public int CommentCount { get; set; }
    public bool IsFollowing { get; set; }
    public bool IsSelf { get; set; }
    public bool IsAuthenticated { get; set; }
}

// Bí danh tránh trùng tên với static class Roles trong namespace Models.
internal static class Roles2
{
    public const string Admin = "Admin";
    public const string Moderator = "Moderator";
    public const string Member = "Member";
}
