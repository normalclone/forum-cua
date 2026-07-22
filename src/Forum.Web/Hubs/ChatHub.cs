using System.Collections.Concurrent;
using Forum.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Forum.Web.Hubs;

/// <summary>Chat 1-1 real-time + trạng thái online cơ bản.</summary>
[Authorize]
public class ChatHub : Hub
{
    // CỐ Ý per-process: bản đồ presence cục bộ theo tiến trình, chỉ đúng khi chạy ĐƠN
    // instance. Khi scale-out phải chuyển presence sang shared store (Redis) và thêm
    // SignalR backplane (AddStackExchangeRedis / AddAzureSignalR) ở Program.cs.
    private static readonly ConcurrentDictionary<int, int> Online = new(); // userId -> số kết nối

    private readonly ApplicationDbContext _db;
    public ChatHub(ApplicationDbContext db) => _db = db;

    public override async Task OnConnectedAsync()
    {
        var uid = Context.User!.UserId();
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{uid}");
        var count = Online.AddOrUpdate(uid, 1, (_, c) => c + 1);
        if (count == 1)
            await Clients.Others.SendAsync("presence", new { userId = uid, online = true });
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? ex)
    {
        var uid = Context.User!.UserId();
        if (Online.AddOrUpdate(uid, 0, (_, c) => Math.Max(0, c - 1)) == 0)
        {
            Online.TryRemove(uid, out _);
            await Clients.Others.SendAsync("presence", new { userId = uid, online = false });
        }
        await base.OnDisconnectedAsync(ex);
    }

    public static bool IsOnline(int userId) => Online.ContainsKey(userId);

    public async Task SendMessage(int conversationId, string body,
        string? attachmentUrl = null, string? attachmentName = null, string? attachmentType = null, bool isImage = false)
    {
        body = (body ?? "").Trim();
        if (body.Length > 4000) return;
        var hasAttach = !string.IsNullOrEmpty(attachmentUrl);
        if (body.Length == 0 && !hasAttach) return;
        // Chỉ nhận đính kèm là tệp đã upload nội bộ (chống chèn URL tuỳ ý).
        if (hasAttach && !attachmentUrl!.StartsWith("/uploads/", StringComparison.Ordinal)) return;

        var uid = Context.User!.UserId();
        var participants = await _db.ConversationParticipants
            .Where(p => p.ConversationId == conversationId).Select(p => p.UserId).ToListAsync();
        if (!participants.Contains(uid)) return; // không thuộc hội thoại

        var now = DateTime.UtcNow;
        var msg = new ChatMessage
        {
            ConversationId = conversationId, SenderId = uid, Body = body, CreatedAt = now,
            AttachmentUrl = hasAttach ? attachmentUrl : null,
            AttachmentName = hasAttach ? attachmentName : null,
            AttachmentType = hasAttach ? attachmentType : null,
            IsImageAttachment = hasAttach && isImage
        };
        _db.ChatMessages.Add(msg);
        var conv = await _db.Conversations.FindAsync(conversationId);
        if (conv != null) conv.LastMessageAt = now;
        await _db.SaveChangesAsync();

        var sender = await _db.Users.FindAsync(uid);
        var payload = new
        {
            id = msg.Id,
            conversationId,
            senderId = uid,
            senderName = sender?.DisplayName ?? "",
            body,
            attachmentUrl = msg.AttachmentUrl,
            attachmentName = msg.AttachmentName,
            attachmentType = msg.AttachmentType,
            isImage = msg.IsImageAttachment,
            createdAt = now
        };
        foreach (var pid in participants)
            await Clients.Group($"user-{pid}").SendAsync("message", payload);
    }
}
