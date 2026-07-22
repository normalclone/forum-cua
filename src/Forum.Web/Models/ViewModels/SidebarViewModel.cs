namespace Forum.Web.Models.ViewModels;

public record CategoryCount(Category Category, int TopicCount);

public class SidebarViewModel
{
    public List<CategoryCount> Categories { get; set; } = new();
    public List<Tag> Tags { get; set; } = new();
    public List<ApplicationUser> TopMembers { get; set; } = new();
    public int TotalTopics { get; set; }
    public int TotalMembers { get; set; }
    public int TotalComments { get; set; }
    public string? Active { get; set; }
}
