namespace Forum.Web.Models;

/// <summary>Cuộc hội thoại nhắn tin (hiện hỗ trợ 1-1, schema cho phép mở rộng nhóm).</summary>
public class Conversation
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastMessageAt { get; set; }

    public ICollection<ConversationParticipant> Participants { get; set; } = new List<ConversationParticipant>();
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}

public class ConversationParticipant
{
    public int ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public DateTime? LastReadAt { get; set; }   // để tính số tin chưa đọc
}

/// <summary>Tin nhắn trong "Chat chung" toàn diễn đàn (shoutbox) — ai cũng xem được.</summary>
public class ShoutMessage
{
    public int Id { get; set; }
    public int SenderId { get; set; }
    public ApplicationUser Sender { get; set; } = null!;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ChatMessage
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;

    public int SenderId { get; set; }
    public ApplicationUser Sender { get; set; } = null!;

    public string Body { get; set; } = string.Empty;

    // Đính kèm tuỳ chọn (ảnh dán/clipboard hoặc file pdf/docx/excel).
    public string? AttachmentUrl { get; set; }
    public string? AttachmentName { get; set; }
    public string? AttachmentType { get; set; }
    public bool IsImageAttachment { get; set; }

    public DateTime CreatedAt { get; set; }
}
