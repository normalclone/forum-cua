using Microsoft.EntityFrameworkCore;

namespace Forum.Web.Services;

/// <summary>Tổng hợp cảm xúc cho 1 đối tượng: đếm theo emoji + tập emoji người hiện tại đã thả.</summary>
public record ReactionSummary(Dictionary<string, int> Counts, HashSet<string> Mine)
{
    public static ReactionSummary Empty => new(new(), new());
}

public interface IEngagementService
{
    /// <summary>Bộ emoji được phép (chống nhập tùy tiện).</summary>
    IReadOnlyList<string> AllowedEmojis { get; }

    Task<ReactionSummary> ToggleReactionAsync(int userId, bool isComment, int targetId, string emoji);
    Task<ReactionSummary> GetForTopicAsync(int topicId, int userId);
    Task<Dictionary<int, ReactionSummary>> GetForCommentsAsync(IReadOnlyCollection<int> commentIds, int userId);

    Task<bool> ToggleTagSubscriptionAsync(int userId, int tagId);
    Task<HashSet<int>> SubscribedTagIdsAsync(int userId);

    /// <summary>Chọn/bỏ chọn đáp án được chấp nhận. Trả về (ok, acceptedCommentId hiện tại).</summary>
    Task<(bool ok, int? acceptedId)> ToggleAcceptedAnswerAsync(int topicId, int commentId, int actingUserId);

    Task<bool> ToggleBlockAsync(int blockerId, int blockedId);   // trả về trạng thái đã chặn
    Task<bool> IsBlockedAsync(int blockerId, int blockedId);
    Task<HashSet<int>> BlockedIdsAsync(int blockerId);
}

public class EngagementService : IEngagementService
{
    private readonly ApplicationDbContext _db;
    private readonly IReputationService _reputation;

    public EngagementService(ApplicationDbContext db, IReputationService reputation)
    {
        _db = db; _reputation = reputation;
    }

    private static readonly string[] Emojis = { "👍", "❤️", "😄", "😮", "🎉", "🙏" };
    public IReadOnlyList<string> AllowedEmojis => Emojis;

    public async Task<ReactionSummary> ToggleReactionAsync(int userId, bool isComment, int targetId, string emoji)
    {
        if (!Emojis.Contains(emoji)) throw new InvalidOperationException("Cảm xúc không hợp lệ.");

        int? topicId = isComment ? null : targetId;
        int? commentId = isComment ? targetId : null;

        // Đối tượng phải tồn tại (và chưa xóa).
        var exists = isComment
            ? await _db.Comments.AnyAsync(c => c.Id == targetId)
            : await _db.Topics.AnyAsync(t => t.Id == targetId);
        if (!exists) throw new InvalidOperationException("Nội dung không tồn tại.");

        var existing = await _db.Reactions.FirstOrDefaultAsync(r =>
            r.UserId == userId && r.TopicId == topicId && r.CommentId == commentId && r.Emoji == emoji);
        if (existing != null) _db.Reactions.Remove(existing);
        else _db.Reactions.Add(new Reaction { UserId = userId, TopicId = topicId, CommentId = commentId, Emoji = emoji, CreatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        return await LoadSummaryAsync(isComment, targetId, userId);
    }

    private async Task<ReactionSummary> LoadSummaryAsync(bool isComment, int targetId, int userId)
    {
        var q = isComment ? _db.Reactions.Where(r => r.CommentId == targetId)
                          : _db.Reactions.Where(r => r.TopicId == targetId);
        var rows = await q.Select(r => new { r.Emoji, r.UserId }).ToListAsync();
        var counts = rows.GroupBy(r => r.Emoji).ToDictionary(g => g.Key, g => g.Count());
        var mine = rows.Where(r => r.UserId == userId).Select(r => r.Emoji).ToHashSet();
        return new ReactionSummary(counts, mine);
    }

    public Task<ReactionSummary> GetForTopicAsync(int topicId, int userId) => LoadSummaryAsync(false, topicId, userId);

    public async Task<Dictionary<int, ReactionSummary>> GetForCommentsAsync(IReadOnlyCollection<int> commentIds, int userId)
    {
        var result = new Dictionary<int, ReactionSummary>();
        if (commentIds.Count == 0) return result;
        var rows = await _db.Reactions
            .Where(r => r.CommentId != null && commentIds.Contains(r.CommentId!.Value))
            .Select(r => new { CommentId = r.CommentId!.Value, r.Emoji, r.UserId })
            .ToListAsync();
        foreach (var g in rows.GroupBy(r => r.CommentId))
        {
            var counts = g.GroupBy(x => x.Emoji).ToDictionary(x => x.Key, x => x.Count());
            var mine = g.Where(x => x.UserId == userId).Select(x => x.Emoji).ToHashSet();
            result[g.Key] = new ReactionSummary(counts, mine);
        }
        return result;
    }

    public async Task<bool> ToggleTagSubscriptionAsync(int userId, int tagId)
    {
        if (!await _db.Tags.AnyAsync(t => t.Id == tagId)) throw new InvalidOperationException("Thẻ không tồn tại.");
        var existing = await _db.TagSubscriptions.FindAsync(userId, tagId);
        if (existing != null) { _db.TagSubscriptions.Remove(existing); await _db.SaveChangesAsync(); return false; }
        _db.TagSubscriptions.Add(new TagSubscription { UserId = userId, TagId = tagId, CreatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<HashSet<int>> SubscribedTagIdsAsync(int userId)
        => (await _db.TagSubscriptions.Where(s => s.UserId == userId).Select(s => s.TagId).ToListAsync()).ToHashSet();

    public async Task<(bool ok, int? acceptedId)> ToggleAcceptedAnswerAsync(int topicId, int commentId, int actingUserId)
    {
        var topic = await _db.Topics.FirstOrDefaultAsync(t => t.Id == topicId);
        if (topic is null) return (false, null);
        var comment = await _db.Comments.FirstOrDefaultAsync(c => c.Id == commentId && c.TopicId == topicId);
        if (comment is null) return (false, topic.AcceptedAnswerId);

        if (topic.AcceptedAnswerId == commentId)
        {
            // Bỏ chọn → thu hồi uy tín đã tặng.
            topic.AcceptedAnswerId = null;
            if (comment.AuthorId != topic.AuthorId)
                await _reputation.ApplyReputationDeltaAsync(comment.AuthorId, -ReputationPoints.AcceptedAnswer);
        }
        else
        {
            // Nếu trước đó có đáp án khác → thu hồi uy tín của đáp án cũ.
            if (topic.AcceptedAnswerId is int oldId)
            {
                var old = await _db.Comments.FirstOrDefaultAsync(c => c.Id == oldId);
                if (old != null && old.AuthorId != topic.AuthorId)
                    await _reputation.ApplyReputationDeltaAsync(old.AuthorId, -ReputationPoints.AcceptedAnswer);
            }
            topic.AcceptedAnswerId = commentId;
            if (comment.AuthorId != topic.AuthorId)
                await _reputation.ApplyReputationDeltaAsync(comment.AuthorId, ReputationPoints.AcceptedAnswer);
        }
        await _db.SaveChangesAsync();
        return (true, topic.AcceptedAnswerId);
    }

    public async Task<bool> ToggleBlockAsync(int blockerId, int blockedId)
    {
        if (blockerId == blockedId) throw new InvalidOperationException("Không thể tự chặn chính mình.");
        var existing = await _db.UserBlocks.FindAsync(blockerId, blockedId);
        if (existing != null) { _db.UserBlocks.Remove(existing); await _db.SaveChangesAsync(); return false; }
        if (!await _db.Users.AnyAsync(u => u.Id == blockedId)) throw new InvalidOperationException("Không tìm thấy thành viên.");
        _db.UserBlocks.Add(new UserBlock { BlockerId = blockerId, BlockedId = blockedId, CreatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();
        return true;
    }

    public Task<bool> IsBlockedAsync(int blockerId, int blockedId)
        => _db.UserBlocks.AnyAsync(b => b.BlockerId == blockerId && b.BlockedId == blockedId);

    public async Task<HashSet<int>> BlockedIdsAsync(int blockerId)
        => (await _db.UserBlocks.Where(b => b.BlockerId == blockerId).Select(b => b.BlockedId).ToListAsync()).ToHashSet();
}
