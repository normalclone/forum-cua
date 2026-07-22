using Microsoft.EntityFrameworkCore;

namespace Forum.Web.Services;

public record VoteResult(int Score, int UpvoteCount, int DownvoteCount, int UserVote);

public interface IVoteService
{
    Task<VoteResult> VoteTopicAsync(int topicId, int userId, int value);
    Task<VoteResult> VoteCommentAsync(int commentId, int userId, int value);
    Task<int> GetUserTopicVoteAsync(int topicId, int userId);
    Task<int> GetUserCommentVoteAsync(int commentId, int userId);
}

public class VoteService : IVoteService
{
    private readonly ApplicationDbContext _db;
    private readonly IReputationService _reputation;

    public VoteService(ApplicationDbContext db, IReputationService reputation)
    {
        _db = db;
        _reputation = reputation;
    }

    public Task<int> GetUserTopicVoteAsync(int topicId, int userId)
        => _db.TopicVotes.Where(v => v.TopicId == topicId && v.UserId == userId)
            .Select(v => (int)v.Value).FirstOrDefaultAsync();

    public Task<int> GetUserCommentVoteAsync(int commentId, int userId)
        => _db.CommentVotes.Where(v => v.CommentId == commentId && v.UserId == userId)
            .Select(v => (int)v.Value).FirstOrDefaultAsync();

    public async Task<VoteResult> VoteTopicAsync(int topicId, int userId, int value)
    {
        value = Normalize(value);
        var topic = await _db.Topics.FirstOrDefaultAsync(t => t.Id == topicId && !t.IsDeleted)
                    ?? throw new InvalidOperationException("Chủ đề không tồn tại.");
        if (topic.AuthorId == userId)
            throw new InvalidOperationException("Không thể tự bình chọn cho nội dung của mình.");
        var existing = await _db.TopicVotes.FirstOrDefaultAsync(v => v.TopicId == topicId && v.UserId == userId);

        var oldValue = existing?.Value ?? 0;
        var newValue = (oldValue == value) ? (short)0 : (short)value; // bấm lại cùng chiều = bỏ vote

        ApplyDelta(topic.UpvoteCount, topic.DownvoteCount, oldValue, newValue,
            out var up, out var down);
        topic.UpvoteCount = up;
        topic.DownvoteCount = down;
        topic.Score = up - down;
        topic.HotScore = Ranking.HotScore(topic.Score, topic.CreatedAt);

        UpsertVote(existing, newValue, () => new TopicVote
        {
            TopicId = topicId, UserId = userId, Value = newValue, CreatedAt = DateTime.UtcNow
        }, _db.TopicVotes);

        // Uy tín cho tác giả (không tự cộng cho chính mình).
        if (topic.AuthorId != userId)
        {
            var repDelta = Reward(newValue, ReputationPoints.TopicUpvote, ReputationPoints.TopicDownvote)
                         - Reward(oldValue, ReputationPoints.TopicUpvote, ReputationPoints.TopicDownvote);
            await AdjustReputationAsync(topic.AuthorId, repDelta);
        }

        await _db.SaveChangesAsync();

        if (topic.AuthorId != userId && newValue == 1)
            await _reputation.CheckAndAwardBadgesAsync(topic.AuthorId);

        return new VoteResult(topic.Score, topic.UpvoteCount, topic.DownvoteCount, newValue);
    }

    public async Task<VoteResult> VoteCommentAsync(int commentId, int userId, int value)
    {
        value = Normalize(value);
        var comment = await _db.Comments.FirstOrDefaultAsync(c => c.Id == commentId && !c.IsDeleted)
                      ?? throw new InvalidOperationException("Bình luận không tồn tại.");
        if (comment.AuthorId == userId)
            throw new InvalidOperationException("Không thể tự bình chọn cho nội dung của mình.");
        var existing = await _db.CommentVotes.FirstOrDefaultAsync(v => v.CommentId == commentId && v.UserId == userId);

        var oldValue = existing?.Value ?? 0;
        var newValue = (oldValue == value) ? (short)0 : (short)value;

        ApplyDelta(comment.UpvoteCount, comment.DownvoteCount, oldValue, newValue, out var up, out var down);
        comment.UpvoteCount = up;
        comment.DownvoteCount = down;
        comment.Score = up - down;

        UpsertVote(existing, newValue, () => new CommentVote
        {
            CommentId = commentId, UserId = userId, Value = newValue, CreatedAt = DateTime.UtcNow
        }, _db.CommentVotes);

        if (comment.AuthorId != userId)
        {
            var repDelta = Reward(newValue, ReputationPoints.CommentUpvote, ReputationPoints.CommentDownvote)
                         - Reward(oldValue, ReputationPoints.CommentUpvote, ReputationPoints.CommentDownvote);
            await AdjustReputationAsync(comment.AuthorId, repDelta);
        }

        await _db.SaveChangesAsync();
        return new VoteResult(comment.Score, comment.UpvoteCount, comment.DownvoteCount, newValue);
    }

    // ---- helpers ----

    private static int Normalize(int value) => value > 0 ? 1 : value < 0 ? -1 : 0;

    private static int Reward(int value, int up, int down) => value == 1 ? up : value == -1 ? down : 0;

    private static void ApplyDelta(int up, int down, short oldValue, short newValue, out int newUp, out int newDown)
    {
        if (oldValue == 1) up--;
        else if (oldValue == -1) down--;
        if (newValue == 1) up++;
        else if (newValue == -1) down++;
        newUp = Math.Max(0, up);
        newDown = Math.Max(0, down);
    }

    private void UpsertVote<T>(T? existing, short newValue, Func<T> create, DbSet<T> set) where T : class
    {
        if (existing is null)
        {
            if (newValue != 0) set.Add(create());
        }
        else if (newValue == 0)
        {
            set.Remove(existing);
        }
        else
        {
            // cập nhật giá trị qua reflection-free: dùng thuộc tính Value chung qua dynamic.
            ((dynamic)existing).Value = newValue;
        }
    }

    // Ghi uy tín tập trung ở ReputationService (một nơi kẹp sàn duy nhất).
    // Thay đổi được lưu bởi SaveChangesAsync chung ở cuối VoteTopic/VoteCommentAsync.
    private Task AdjustReputationAsync(int userId, int delta)
        => _reputation.ApplyReputationDeltaAsync(userId, delta);
}
