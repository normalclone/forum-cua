namespace Forum.Web.Models.ViewModels;

public record LeaderRow(ApplicationUser User, int Rank, int TopicCount, int BadgeCount);

public class ModerationViewModel
{
    public List<Report> PendingReports { get; set; } = new();
    public List<Topic> PendingTopics { get; set; } = new();   // bài chờ duyệt (danh mục yêu cầu duyệt)
    public List<ModerationLog> AuditLog { get; set; } = new();
    public int TotalTopics { get; set; }
    public int TotalComments { get; set; }
    public int PendingReportCount { get; set; }
    public int PendingApprovalCount { get; set; }
}
