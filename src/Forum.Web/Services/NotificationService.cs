using System.Text.RegularExpressions;
using Forum.Web.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Forum.Web.Services;

public interface INotificationService
{
    Task<Notification?> CreateAsync(int recipientId, NotificationType type, int? actorId = null,
        int? topicId = null, int? commentId = null, string? message = null, string? url = null);

    Task NotifyReplyAsync(Topic topic, Comment comment, int actorId);
    Task<List<string>> NotifyMentionsAsync(string body, int actorId, int topicId, int? commentId, string url);
    Task NotifyFollowAsync(int followerId, int followeeId);
    Task NotifyNewTopicToTagFollowersAsync(int topicId, int authorId);
    Task NotifyBadgeAsync(int userId, string badgeName);
    Task NotifyModerationAsync(int userId, string message, string url);
    Task NotifyMessageAsync(int recipientId, int senderId, int conversationId, string preview);

    Task<int> UnreadCountAsync(int userId);
    Task<List<Notification>> RecentAsync(int userId, int take = 15);
    Task MarkReadAsync(int userId, int notificationId);
    Task MarkAllReadAsync(int userId);
}

public partial class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly IForumUrlService _url;
    private readonly IHubContext<ForumHub> _hub;

    public NotificationService(ApplicationDbContext db, IForumUrlService url, IHubContext<ForumHub> hub)
    {
        _db = db;
        _url = url;
        _hub = hub;
    }

    public async Task<Notification?> CreateAsync(int recipientId, NotificationType type, int? actorId = null,
        int? topicId = null, int? commentId = null, string? message = null, string? url = null)
    {
        if (actorId.HasValue && actorId.Value == recipientId)
            return null; // không tự thông báo cho chính mình

        var n = new Notification
        {
            RecipientId = recipientId,
            ActorId = actorId,
            Type = type,
            TopicId = topicId,
            CommentId = commentId,
            Message = message,
            Url = url,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
        _db.Notifications.Add(n);
        await _db.SaveChangesAsync();

        // Đẩy real-time số chưa đọc tới người nhận.
        var unread = await _db.Notifications.CountAsync(x => x.RecipientId == recipientId && !x.IsRead);
        await _hub.Clients.Group($"user-{recipientId}").SendAsync("notify", new { count = unread, type = type.ToString() });
        return n;
    }

    public async Task NotifyReplyAsync(Topic topic, Comment comment, int actorId)
    {
        var url = _url.Topic(topic) + "#comment-" + comment.Id;
        var recipients = new HashSet<int>();

        // Tác giả chủ đề.
        recipients.Add(topic.AuthorId);

        // Tác giả bình luận cha (nếu trả lời lồng).
        if (comment.ParentCommentId.HasValue)
        {
            var parentAuthor = await _db.Comments
                .Where(c => c.Id == comment.ParentCommentId.Value)
                .Select(c => (int?)c.AuthorId).FirstOrDefaultAsync();
            if (parentAuthor.HasValue) recipients.Add(parentAuthor.Value);
        }

        // Người theo dõi chủ đề.
        var subscribers = await _db.TopicSubscriptions
            .Where(s => s.TopicId == topic.Id)
            .Select(s => s.UserId).ToListAsync();
        foreach (var s in subscribers) recipients.Add(s);

        recipients.Remove(actorId); // bỏ chính người trả lời

        await FanOutAsync(recipients.ToList(), NotificationType.Reply, actorId, topic.Id, comment.Id, null, url);
    }

    public async Task<List<string>> NotifyMentionsAsync(string body, int actorId, int topicId, int? commentId, string url)
    {
        var mentioned = new List<string>();
        if (string.IsNullOrWhiteSpace(body)) return mentioned;

        var names = MentionPattern().Matches(body)
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (names.Count == 0) return mentioned;

        var users = await _db.Users
            .Where(u => names.Contains(u.UserName!))
            .Select(u => new { u.Id, u.UserName })
            .ToListAsync();

        var recipients = new List<int>();
        foreach (var u in users)
        {
            if (u.Id == actorId) continue; // không tự thông báo cho chính mình
            recipients.Add(u.Id);
            mentioned.Add(u.UserName!);
        }

        await FanOutAsync(recipients, NotificationType.Mention, actorId, topicId, commentId, null, url);
        return mentioned;
    }

    /// <summary>
    /// Tạo thông báo cho nhiều người nhận trong 1 lượt: 1 INSERT gộp (AddRange),
    /// 1 truy vấn gom số chưa đọc, và đẩy SignalR song song — thay cho N+1 round-trip.
    /// Người gọi phải tự loại bỏ actor và khử trùng lặp trước khi truyền vào.
    /// </summary>
    private async Task FanOutAsync(List<int> recipientIds, NotificationType type, int? actorId,
        int? topicId, int? commentId, string? message, string? url)
    {
        if (recipientIds.Count == 0) return;

        var now = DateTime.UtcNow;
        var notifs = recipientIds.Select(rid => new Notification
        {
            RecipientId = rid,
            ActorId = actorId,
            Type = type,
            TopicId = topicId,
            CommentId = commentId,
            Message = message,
            Url = url,
            IsRead = false,
            CreatedAt = now
        }).ToList();
        _db.Notifications.AddRange(notifs);
        await _db.SaveChangesAsync();

        // Một truy vấn gom số chưa đọc cho tất cả người nhận.
        var unreadCounts = await _db.Notifications
            .Where(x => recipientIds.Contains(x.RecipientId) && !x.IsRead)
            .GroupBy(x => x.RecipientId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        // Đẩy real-time số chưa đọc tới từng người nhận, song song.
        var typeStr = type.ToString();
        await Task.WhenAll(recipientIds.Select(rid =>
            _hub.Clients.Group($"user-{rid}").SendAsync("notify",
                new { count = unreadCounts.GetValueOrDefault(rid, 0), type = typeStr })));
    }

    public async Task NotifyNewTopicToTagFollowersAsync(int topicId, int authorId)
    {
        var topic = await _db.Topics.Include(t => t.TopicTags)
            .Where(t => t.Id == topicId).Select(t => new { t.Id, t.Title, t.Slug, TagIds = t.TopicTags.Select(tt => tt.TagId).ToList() })
            .FirstOrDefaultAsync();
        if (topic is null || topic.TagIds.Count == 0) return;

        var recipients = await _db.TagSubscriptions
            .Where(s => topic.TagIds.Contains(s.TagId) && s.UserId != authorId)
            .Select(s => s.UserId).Distinct().ToListAsync();
        if (recipients.Count == 0) return;

        var url = $"/chu-de/{topic.Id}/{topic.Slug}";
        await FanOutAsync(recipients, NotificationType.TagTopic, authorId, topic.Id, null,
            $"Chủ đề mới thuộc thẻ bạn theo dõi: \"{topic.Title}\"", url);
    }

    public async Task NotifyFollowAsync(int followerId, int followeeId)
        => await CreateAsync(followeeId, NotificationType.Follow, followerId, url: _url.User(
            await _db.Users.Where(u => u.Id == followerId).Select(u => u.UserName!).FirstOrDefaultAsync() ?? ""));

    public async Task NotifyBadgeAsync(int userId, string badgeName)
        => await CreateAsync(userId, NotificationType.Badge, null, message: $"Bạn vừa nhận huy hiệu \"{badgeName}\"!",
            url: _url.User(await _db.Users.Where(u => u.Id == userId).Select(u => u.UserName!).FirstOrDefaultAsync() ?? ""));

    public async Task NotifyMessageAsync(int recipientId, int senderId, int conversationId, string preview)
        => await CreateAsync(recipientId, NotificationType.Message, senderId, message: preview,
            url: $"/tin-nhan/{conversationId}");

    public async Task NotifyModerationAsync(int userId, string message, string url)
        => await CreateAsync(userId, NotificationType.Moderation, null, message: message, url: url);

    public Task<int> UnreadCountAsync(int userId)
        => _db.Notifications.CountAsync(n => n.RecipientId == userId && !n.IsRead);

    public Task<List<Notification>> RecentAsync(int userId, int take = 15)
        => _db.Notifications
            .Include(n => n.Actor)
            .Include(n => n.Topic)
            .Where(n => n.RecipientId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .ToListAsync();

    public async Task MarkReadAsync(int userId, int notificationId)
    {
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == notificationId && x.RecipientId == userId);
        if (n is { IsRead: false })
        {
            n.IsRead = true;
            await _db.SaveChangesAsync();
        }
    }

    public async Task MarkAllReadAsync(int userId)
    {
        var unread = await _db.Notifications.Where(n => n.RecipientId == userId && !n.IsRead).ToListAsync();
        foreach (var n in unread) n.IsRead = true;
        if (unread.Count > 0) await _db.SaveChangesAsync();
    }

    [GeneratedRegex(@"@([A-Za-z0-9_\.]{2,30})")]
    private static partial Regex MentionPattern();
}
