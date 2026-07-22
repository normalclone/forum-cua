using Forum.Web.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Forum.Web.Services;

public interface ITopicService
{
    Task<Topic> CreateAsync(int authorId, int categoryId, string title, string body,
        IEnumerable<string> tagNames, bool isQuestion);
    Task<Topic?> UpdateAsync(int topicId, int editorId, string title, string body, IEnumerable<string> tagNames);
    Task<bool> SoftDeleteOwnAsync(int topicId, int userId);
    Task<Topic?> GetForDetailAsync(int id);
    Task IncrementViewAsync(int topicId);
    Task<bool> ToggleBookmarkAsync(int topicId, int userId);
    Task<bool> ToggleSubscriptionAsync(int topicId, int userId);
    Task<int[]> AttachTagsAsync(IEnumerable<string> tagNames);
}

public class TopicService : ITopicService
{
    private readonly ApplicationDbContext _db;
    private readonly ISlugService _slug;
    private readonly INotificationService _notifications;
    private readonly IReputationService _reputation;
    private readonly IHubContext<ForumHub> _hub;
    private readonly ISiteSettingService _settings;
    private readonly IWordFilterService _filter;

    public TopicService(ApplicationDbContext db, ISlugService slug,
        INotificationService notifications, IReputationService reputation, IHubContext<ForumHub> hub,
        ISiteSettingService settings, IWordFilterService filter)
    {
        _db = db;
        _slug = slug;
        _notifications = notifications;
        _reputation = reputation;
        _hub = hub;
        _settings = settings;
        _filter = filter;
    }

    public async Task<Topic> CreateAsync(int authorId, int categoryId, string title, string body,
        IEnumerable<string> tagNames, bool isQuestion)
    {
        var now = DateTime.UtcNow;
        var cat = await _db.Categories.FindAsync(categoryId);
        var approved = cat?.RequireApproval != true;   // danh mục yêu cầu duyệt → chờ kiểm duyệt

        // Tự động kiểm duyệt (chỉ khi bài đang ở trạng thái sẽ hiển thị ngay).
        if (approved && _settings.GetBool(SettingKeys.AutomodNewUser, false))
        {
            var author0 = await _db.Users.FindAsync(authorId);
            var days = _settings.GetInt(SettingKeys.AutomodNewUserDays, 3);
            if (author0 != null && (now - author0.CreatedAt).TotalDays < days) approved = false;
        }
        if (approved && _settings.GetBool(SettingKeys.AutomodSpam, true) && _filter.LooksLikeSpam(body))
            approved = false;

        var topic = new Topic
        {
            Title = title.Trim(),
            Slug = _slug.Generate(title, 120),
            Body = body,
            CategoryId = categoryId,
            AuthorId = authorId,
            CreatedAt = now,
            LastActivityAt = now,
            IsQuestion = isQuestion,
            IsApproved = approved,
            HotScore = Ranking.HotScore(0, now)
        };

        var tagIds = await ResolveTagsAsync(tagNames, create: true);
        foreach (var tagId in tagIds)
            topic.TopicTags.Add(new TopicTag { TagId = tagId });

        _db.Topics.Add(topic);
        await _db.SaveChangesAsync();

        // Tự theo dõi chủ đề của mình + ghi hoạt động.
        _db.TopicSubscriptions.Add(new TopicSubscription { TopicId = topic.Id, UserId = authorId, CreatedAt = now });
        _db.UserActivities.Add(new UserActivity { UserId = authorId, Type = ActivityType.CreatedTopic, TopicId = topic.Id, CreatedAt = now });
        await _db.SaveChangesAsync();

        await _reputation.CheckAndAwardBadgesAsync(authorId);

        // Chỉ thông báo @mention + đẩy lên board khi bài đã hiển thị (đã duyệt).
        // Bài chờ duyệt sẽ được đẩy khi kiểm duyệt viên phê duyệt (ModerationService.ApproveTopicAsync).
        if (approved)
        {
            await _notifications.NotifyMentionsAsync(body, authorId, topic.Id, null, $"/chu-de/{topic.Id}/{topic.Slug}");
            var author = await _db.Users.FindAsync(authorId);
            await _hub.Clients.All.SendAsync("newTopic", new
            {
                id = topic.Id,
                title = topic.Title,
                url = $"/chu-de/{topic.Id}/{topic.Slug}",
                author = author?.DisplayName,
                category = cat?.Name
            });
        }
        return topic;
    }

    public async Task<Topic?> UpdateAsync(int topicId, int editorId, string title, string body, IEnumerable<string> tagNames)
    {
        var topic = await _db.Topics.Include(t => t.TopicTags).FirstOrDefaultAsync(t => t.Id == topicId && !t.IsDeleted);
        if (topic is null) return null;

        topic.Title = title.Trim();
        topic.Slug = _slug.Generate(title, 120);
        topic.Body = body;
        topic.UpdatedAt = DateTime.UtcNow;

        // Cập nhật tag: tính chênh lệch.
        var newTagIds = (await ResolveTagsAsync(tagNames, create: true)).ToHashSet();
        var currentTagIds = topic.TopicTags.Select(tt => tt.TagId).ToHashSet();

        foreach (var removed in currentTagIds.Except(newTagIds))
        {
            var tt = topic.TopicTags.First(x => x.TagId == removed);
            _db.TopicTags.Remove(tt);
            var tag = await _db.Tags.FindAsync(removed);
            if (tag is not null) tag.UseCount = Math.Max(0, tag.UseCount - 1);
        }
        foreach (var added in newTagIds.Except(currentTagIds))
            topic.TopicTags.Add(new TopicTag { TopicId = topic.Id, TagId = added });

        await _db.SaveChangesAsync();
        return topic;
    }

    public async Task<bool> SoftDeleteOwnAsync(int topicId, int userId)
    {
        var topic = await _db.Topics.FirstOrDefaultAsync(t => t.Id == topicId);
        if (topic is null || topic.AuthorId != userId) return false;
        topic.IsDeleted = true;
        await _db.SaveChangesAsync();
        // Reconcile uy tín: vote trên chủ đề đã xóa không còn được tính.
        await _reputation.RecalculateReputationAsync(topic.AuthorId);
        return true;
    }

    public Task<Topic?> GetForDetailAsync(int id)
        => _db.Topics
            .Include(t => t.Author)
            .Include(t => t.Category)
            .Include(t => t.TopicTags).ThenInclude(tt => tt.Tag)
            .Include(t => t.Poll!).ThenInclude(p => p.Options)
            .Include(t => t.Attachments)
            .FirstOrDefaultAsync(t => t.Id == id);

    // UPDATE nguyên tử (không load entity) → tránh lost-update race khi nhiều lượt xem
    // đồng thời, và bỏ được một SELECT + full-row UPDATE mỗi lần mở chủ đề.
    // ExecuteUpdate dịch được cho cả SQLite lẫn SQL Server (giữ trung lập provider).
    public Task IncrementViewAsync(int topicId)
        => _db.Topics
            .Where(t => t.Id == topicId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.ViewCount, t => t.ViewCount + 1));

    public async Task<bool> ToggleBookmarkAsync(int topicId, int userId)
    {
        var existing = await _db.Bookmarks.FindAsync(userId, topicId);
        if (existing is null)
        {
            _db.Bookmarks.Add(new Bookmark { UserId = userId, TopicId = topicId, CreatedAt = DateTime.UtcNow });
            await _db.SaveChangesAsync();
            return true; // đã lưu
        }
        _db.Bookmarks.Remove(existing);
        await _db.SaveChangesAsync();
        return false; // đã bỏ lưu
    }

    public async Task<bool> ToggleSubscriptionAsync(int topicId, int userId)
    {
        var existing = await _db.TopicSubscriptions.FindAsync(userId, topicId);
        if (existing is null)
        {
            _db.TopicSubscriptions.Add(new TopicSubscription { UserId = userId, TopicId = topicId, CreatedAt = DateTime.UtcNow });
            await _db.SaveChangesAsync();
            return true;
        }
        _db.TopicSubscriptions.Remove(existing);
        await _db.SaveChangesAsync();
        return false;
    }

    public async Task<int[]> AttachTagsAsync(IEnumerable<string> tagNames)
        => await ResolveTagsAsync(tagNames, create: true);

    private async Task<int[]> ResolveTagsAsync(IEnumerable<string> tagNames, bool create)
    {
        var cleaned = tagNames
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Where(n => n.Length is >= 1 and <= 40)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();
        if (cleaned.Count == 0) return Array.Empty<int>();

        var bySlug = cleaned.ToDictionary(n => _slug.Generate(n, 50), n => n);
        var slugs = bySlug.Keys.ToList();

        var existing = await _db.Tags.Where(t => slugs.Contains(t.Slug)).ToListAsync();
        var result = new List<int>();

        foreach (var (slug, name) in bySlug)
        {
            var tag = existing.FirstOrDefault(t => t.Slug == slug);
            if (tag is null && create)
            {
                tag = new Tag { Name = name, Slug = slug, UseCount = 0, CreatedAt = DateTime.UtcNow };
                _db.Tags.Add(tag);
                await _db.SaveChangesAsync();
                existing.Add(tag);
            }
            if (tag is not null)
            {
                tag.UseCount++;
                result.Add(tag.Id);
            }
        }
        await _db.SaveChangesAsync();
        return result.ToArray();
    }
}
