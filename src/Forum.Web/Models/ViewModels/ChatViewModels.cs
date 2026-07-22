namespace Forum.Web.Models.ViewModels;

public record ConversationSummary(int Id, ApplicationUser Other, string? LastMessage, DateTime LastAt, bool Online, bool Unread);

public class ChatViewModel
{
    public List<ConversationSummary> Conversations { get; set; } = new();
    public Conversation? Active { get; set; }
    public ApplicationUser? Other { get; set; }
    public List<ChatMessage> Messages { get; set; } = new();
    public int CurrentUserId { get; set; }
    public bool OtherOnline { get; set; }
}
