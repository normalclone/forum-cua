using Microsoft.EntityFrameworkCore;

namespace Forum.Web.Services;

public interface ICommentService
{
    Task<Comment> AddAsync(int topicId, int authorId, int? parentCommentId, string body);
    Task<Comment?> UpdateOwnAsync(int commentId, int userId, string body);
    Task<bool> SoftDeleteOwnAsync(int commentId, int userId);
    /// <summary>Lấy toàn bộ bình luận của chủ đề, đã sắp xếp theo cây (materialized path).</summary>
    Task<List<Comment>> GetThreadAsync(int topicId);
    /// <summary>Tạo path materialized cho một comment mới (dùng cho seed/runtime).</summary>
    static string BuildPath(string? parentPath, int id)
        => string.IsNullOrEmpty(parentPath) ? id.ToString("D7") : $"{parentPath}/{id:D7}";
}

public class CommentService : ICommentService
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IReputationService _reputation;

    public CommentService(ApplicationDbContext db, INotificationService notifications, IReputationService reputation)
    {
        _db = db;
        _notifications = notifications;
        _reputation = reputation;
    }

    public async Task<Comment> AddAsync(int topicId, int authorId, int? parentCommentId, string body)
    {
        var topic = await _db.Topics.FirstOrDefaultAsync(t => t.Id == topicId && !t.IsDeleted)
                    ?? throw new InvalidOperationException("Chủ đề không tồn tại.");
        if (topic.IsLocked)
            throw new InvalidOperationException("Chủ đề đã bị khóa, không thể bình luận.");

        Comment? parent = null;
        if (parentCommentId.HasValue)
        {
            parent = await _db.Comments.FirstOrDefaultAsync(c => c.Id == parentCommentId.Value && c.TopicId == topicId);
            if (parent is null) parentCommentId = null;
        }

        var now = DateTime.UtcNow;
        var comment = new Comment
        {
            TopicId = topicId,
            AuthorId = authorId,
            ParentCommentId = parentCommentId,
            Body = body,
            CreatedAt = now,
            Depth = parent?.Depth + 1 ?? 0
        };
        _db.Comments.Add(comment);
        await _db.SaveChangesAsync();

        // Materialized path cần Id -> gán sau khi đã có Id.
        comment.Path = ICommentService.BuildPath(parent?.Path, comment.Id);

        topic.CommentCount++;
        topic.LastActivityAt = now;

        _db.UserActivities.Add(new UserActivity
        {
            UserId = authorId, Type = ActivityType.PostedComment, TopicId = topicId, CommentId = comment.Id, CreatedAt = now
        });
        await _db.SaveChangesAsync();

        var url = $"/chu-de/{topic.Id}/{topic.Slug}#comment-{comment.Id}";
        await _notifications.NotifyReplyAsync(topic, comment, authorId);
        await _notifications.NotifyMentionsAsync(body, authorId, topicId, comment.Id, url);
        await _reputation.CheckAndAwardBadgesAsync(authorId);

        return comment;
    }

    public async Task<Comment?> UpdateOwnAsync(int commentId, int userId, string body)
    {
        var c = await _db.Comments.FirstOrDefaultAsync(x => x.Id == commentId && !x.IsDeleted);
        if (c is null || c.AuthorId != userId) return null;
        c.Body = body;
        c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return c;
    }

    public async Task<bool> SoftDeleteOwnAsync(int commentId, int userId)
    {
        var c = await _db.Comments.FirstOrDefaultAsync(x => x.Id == commentId);
        if (c is null || c.AuthorId != userId) return false;
        c.IsDeleted = true;
        var topic = await _db.Topics.FindAsync(c.TopicId);
        if (topic is not null) topic.CommentCount = Math.Max(0, topic.CommentCount - 1);
        await _db.SaveChangesAsync();
        // Reconcile uy tín: vote trên bình luận đã xóa không còn được tính.
        await _reputation.RecalculateReputationAsync(c.AuthorId);
        return true;
    }

    public Task<List<Comment>> GetThreadAsync(int topicId)
        => _db.Comments
            .IgnoreQueryFilters() // giữ cả bình luận đã soft-delete → placeholder + không mất reply con
            .Include(c => c.Author)
            .Where(c => c.TopicId == topicId)
            .OrderBy(c => c.Path)
            .ToListAsync();
}
