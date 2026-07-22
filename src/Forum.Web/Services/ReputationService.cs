using Microsoft.EntityFrameworkCore;

namespace Forum.Web.Services;

/// <summary>Điểm uy tín cộng/trừ theo loại tương tác.</summary>
public static class ReputationPoints
{
    public const int TopicUpvote = 5;
    public const int TopicDownvote = -2;
    public const int CommentUpvote = 2;
    public const int CommentDownvote = -1;
    public const int AcceptedAnswer = 15;   // được chọn làm đáp án hay
}

/// <summary>Slug các huy hiệu (đồng bộ với dữ liệu seed).</summary>
public static class BadgeSlugs
{
    public const string NewMember = "thanh-vien-moi";
    public const string FirstTopic = "bai-dau-tien";
    public const string Contributor = "nguoi-dong-gop";
    public const string Popular = "duoc-yeu-thich";
    public const string Expert = "chuyen-gia-cua";
    public const string Veteran = "ky-cuu";
}

public interface IReputationService
{
    /// <summary>Kiểm tra tiêu chí và trao các huy hiệu còn thiếu; trả về tên huy hiệu mới trao.</summary>
    Task<List<string>> CheckAndAwardBadgesAsync(int userId);

    /// <summary>Tính lại điểm uy tín từ vote nhận được (dùng cho seed/admin).</summary>
    Task RecalculateReputationAsync(int userId);

    /// <summary>
    /// Cộng/trừ điểm uy tín cho một người dùng theo delta (kẹp sàn ở 0).
    /// Nơi DUY NHẤT ghi <c>User.Reputation</c> theo từng delta để quy tắc kẹp tập trung một chỗ.
    /// KHÔNG tự lưu — thay đổi tham gia vào unit-of-work của caller (SaveChanges chung).
    /// </summary>
    Task ApplyReputationDeltaAsync(int userId, int delta);
}

public class ReputationService : IReputationService
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notifications;

    public ReputationService(ApplicationDbContext db, INotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    public async Task<List<string>> CheckAndAwardBadgesAsync(int userId)
    {
        var awarded = new List<string>();
        var user = await _db.Users.FindAsync(userId);
        if (user is null) return awarded;

        var badges = await _db.Badges.ToDictionaryAsync(b => b.Slug);
        if (badges.Count == 0) return awarded;

        var owned = await _db.UserBadges.Where(ub => ub.UserId == userId)
            .Select(ub => ub.BadgeId).ToListAsync();
        var ownedSet = owned.ToHashSet();

        var topicCount = await _db.Topics.CountAsync(t => t.AuthorId == userId && !t.IsDeleted);
        var commentCount = await _db.Comments.CountAsync(c => c.AuthorId == userId && !c.IsDeleted);
        var maxTopicScore = await _db.Topics.Where(t => t.AuthorId == userId && !t.IsDeleted)
            .Select(t => (int?)t.Score).MaxAsync() ?? 0;
        var ageDays = (DateTime.UtcNow - user.CreatedAt).TotalDays;

        void TryAward(string slug, bool condition)
        {
            if (!condition) return;
            if (!badges.TryGetValue(slug, out var badge)) return;
            if (ownedSet.Contains(badge.Id)) return;
            _db.UserBadges.Add(new UserBadge { UserId = userId, BadgeId = badge.Id, AwardedAt = DateTime.UtcNow });
            ownedSet.Add(badge.Id);
            awarded.Add(badge.Name);
        }

        TryAward(BadgeSlugs.NewMember, true);
        TryAward(BadgeSlugs.FirstTopic, topicCount >= 1);
        TryAward(BadgeSlugs.Contributor, commentCount >= 10);
        TryAward(BadgeSlugs.Popular, maxTopicScore >= 10);
        TryAward(BadgeSlugs.Expert, user.Reputation >= 500);
        TryAward(BadgeSlugs.Veteran, ageDays >= 180);

        if (awarded.Count > 0)
        {
            await _db.SaveChangesAsync();
            foreach (var name in awarded)
                await _notifications.NotifyBadgeAsync(userId, name);
        }
        return awarded;
    }

    public async Task RecalculateReputationAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user is null) return;

        var topicAgg = await _db.Topics.Where(t => t.AuthorId == userId && !t.IsDeleted)
            .Select(t => new { t.UpvoteCount, t.DownvoteCount }).ToListAsync();
        var commentAgg = await _db.Comments.Where(c => c.AuthorId == userId && !c.IsDeleted)
            .Select(c => new { c.UpvoteCount, c.DownvoteCount }).ToListAsync();

        var rep = 0;
        foreach (var t in topicAgg)
            rep += t.UpvoteCount * ReputationPoints.TopicUpvote + t.DownvoteCount * ReputationPoints.TopicDownvote;
        foreach (var c in commentAgg)
            rep += c.UpvoteCount * ReputationPoints.CommentUpvote + c.DownvoteCount * ReputationPoints.CommentDownvote;

        user.Reputation = Math.Max(0, rep);
        await _db.SaveChangesAsync();
    }

    public async Task ApplyReputationDeltaAsync(int userId, int delta)
    {
        if (delta == 0) return;
        var user = await _db.Users.FindAsync(userId);
        if (user is not null)
            user.Reputation = Math.Max(0, user.Reputation + delta);
    }
}
