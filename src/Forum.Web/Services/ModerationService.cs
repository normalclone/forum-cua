using Microsoft.EntityFrameworkCore;

namespace Forum.Web.Services;

public interface IModerationService
{
    Task<bool> SetPinnedAsync(int topicId, int moderatorId, bool pinned);
    Task<bool> SetLockedAsync(int topicId, int moderatorId, bool locked);
    Task<bool> SetFeaturedAsync(int topicId, int moderatorId, bool featured);
    Task<bool> MoveAsync(int topicId, int moderatorId, int newCategoryId);
    Task<bool> DeleteTopicAsync(int topicId, int moderatorId, string? reason);
    Task<bool> RestoreTopicAsync(int topicId, int moderatorId);
    Task<bool> DeleteCommentAsync(int commentId, int moderatorId, string? reason);
    Task<bool> RestoreCommentAsync(int commentId, int moderatorId);

    Task<List<Topic>> PendingTopicsAsync();
    Task<bool> ApproveTopicAsync(int topicId, int moderatorId);
    Task<bool> RejectTopicAsync(int topicId, int moderatorId, string? reason);

    Task<int> CreateReportAsync(int reporterId, ContentTargetType type, int targetId, string reason, string? details);
    Task<bool> ResolveReportAsync(int reportId, int moderatorId, bool dismiss);
    Task<List<Report>> PendingReportsAsync();
    Task<List<ModerationLog>> AuditLogAsync(int take = 100);
    Task<PagedResult<ModerationLog>> AuditLogPagedAsync(string? keyword, int page, int pageSize);

    /// <summary>Người dùng có được kiểm duyệt danh mục này không? Admin: luôn được;
    /// mod chưa phân công danh mục nào: được tất cả; mod đã phân công: chỉ danh mục của mình.</summary>
    Task<bool> CanModerateCategoryAsync(int userId, bool isAdmin, int categoryId);

    Task<bool> WarnUserAsync(int userId, int moderatorId, string reason);
    /// <summary>Đặt mute đến now+hours (hours ≤ 0 = bỏ mute). Trả về MutedUntil mới.</summary>
    Task<DateTimeOffset?> SetMuteAsync(int userId, int moderatorId, int hours, string? reason);
}

public class ModerationService : IModerationService
{
    private readonly ApplicationDbContext _db;
    private readonly IReputationService _reputation;
    private readonly INotificationService _notifications;

    public ModerationService(ApplicationDbContext db, IReputationService reputation, INotificationService notifications)
    {
        _db = db;
        _reputation = reputation;
        _notifications = notifications;
    }

    public async Task<bool> SetPinnedAsync(int topicId, int moderatorId, bool pinned)
    {
        var t = await _db.Topics.FindAsync(topicId);
        if (t is null) return false;
        t.IsPinned = pinned;
        await LogAsync(moderatorId, pinned ? ModerationAction.Pin : ModerationAction.Unpin,
            ContentTargetType.Topic, topicId, t.Title);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetLockedAsync(int topicId, int moderatorId, bool locked)
    {
        var t = await _db.Topics.FindAsync(topicId);
        if (t is null) return false;
        t.IsLocked = locked;
        await LogAsync(moderatorId, locked ? ModerationAction.Lock : ModerationAction.Unlock,
            ContentTargetType.Topic, topicId, t.Title);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetFeaturedAsync(int topicId, int moderatorId, bool featured)
    {
        var t = await _db.Topics.FindAsync(topicId);
        if (t is null) return false;
        t.IsFeatured = featured;
        await LogAsync(moderatorId, featured ? ModerationAction.Feature : ModerationAction.Unfeature,
            ContentTargetType.Topic, topicId, t.Title);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> MoveAsync(int topicId, int moderatorId, int newCategoryId)
    {
        var t = await _db.Topics.FindAsync(topicId);
        if (t is null) return false;
        if (!await _db.Categories.AnyAsync(c => c.Id == newCategoryId)) return false;
        t.CategoryId = newCategoryId;
        await LogAsync(moderatorId, ModerationAction.Move, ContentTargetType.Topic, topicId, t.Title,
            $"-> danh mục #{newCategoryId}");
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteTopicAsync(int topicId, int moderatorId, string? reason)
    {
        var t = await _db.Topics.FindAsync(topicId);
        if (t is null) return false;
        t.IsDeleted = true;
        await LogAsync(moderatorId, ModerationAction.Delete, ContentTargetType.Topic, topicId, t.Title, reason);
        await _db.SaveChangesAsync();
        await _reputation.RecalculateReputationAsync(t.AuthorId); // reconcile uy tín tác giả
        return true;
    }

    public async Task<bool> RestoreTopicAsync(int topicId, int moderatorId)
    {
        // IgnoreQueryFilters: chủ đề đang ở trạng thái đã xóa nên global filter sẽ ẩn nó.
        var t = await _db.Topics.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == topicId);
        if (t is null) return false;
        t.IsDeleted = false;
        await LogAsync(moderatorId, ModerationAction.Restore, ContentTargetType.Topic, topicId, t.Title);
        await _db.SaveChangesAsync();
        await _reputation.RecalculateReputationAsync(t.AuthorId); // khôi phục uy tín tác giả
        return true;
    }

    public async Task<bool> DeleteCommentAsync(int commentId, int moderatorId, string? reason)
    {
        var c = await _db.Comments.FindAsync(commentId);
        if (c is null) return false;
        c.IsDeleted = true;
        await LogAsync(moderatorId, ModerationAction.Delete, ContentTargetType.Comment, commentId, null, reason);
        await _db.SaveChangesAsync();
        await _reputation.RecalculateReputationAsync(c.AuthorId); // reconcile uy tín tác giả
        return true;
    }

    public async Task<bool> RestoreCommentAsync(int commentId, int moderatorId)
    {
        var c = await _db.Comments.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == commentId);
        if (c is null) return false;
        c.IsDeleted = false;
        await LogAsync(moderatorId, ModerationAction.Restore, ContentTargetType.Comment, commentId, null);
        await _db.SaveChangesAsync();
        await _reputation.RecalculateReputationAsync(c.AuthorId);
        return true;
    }

    // ---- Hàng chờ kiểm duyệt (danh mục yêu cầu duyệt) ----
    public Task<List<Topic>> PendingTopicsAsync()
        => _db.Topics
            .Include(t => t.Author)
            .Include(t => t.Category)
            .Where(t => !t.IsApproved && !t.IsDeleted)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();

    public async Task<bool> ApproveTopicAsync(int topicId, int moderatorId)
    {
        var t = await _db.Topics.FindAsync(topicId);
        if (t is null || t.IsApproved) return false;
        t.IsApproved = true;
        t.LastActivityAt = DateTime.UtcNow;
        await LogAsync(moderatorId, ModerationAction.Approve, ContentTargetType.Topic, topicId, t.Title);
        await _db.SaveChangesAsync();
        // Bài đã hiển thị → thông báo tác giả + xử lý @mention.
        await _notifications.NotifyModerationAsync(t.AuthorId,
            $"Chủ đề \"{t.Title}\" của bạn đã được duyệt và hiển thị.", $"/chu-de/{t.Id}/{t.Slug}");
        await _notifications.NotifyMentionsAsync(t.Body, t.AuthorId, t.Id, null, $"/chu-de/{t.Id}/{t.Slug}");
        return true;
    }

    public async Task<bool> RejectTopicAsync(int topicId, int moderatorId, string? reason)
    {
        var t = await _db.Topics.FindAsync(topicId);
        if (t is null) return false;
        t.IsDeleted = true;   // bài bị từ chối → ẩn (soft-delete)
        await LogAsync(moderatorId, ModerationAction.Reject, ContentTargetType.Topic, topicId, t.Title, reason);
        await _db.SaveChangesAsync();
        await _notifications.NotifyModerationAsync(t.AuthorId,
            $"Chủ đề \"{t.Title}\" của bạn không được duyệt." + (string.IsNullOrWhiteSpace(reason) ? "" : $" Lý do: {reason}"),
            "/bang-tin");
        return true;
    }

    public async Task<int> CreateReportAsync(int reporterId, ContentTargetType type, int targetId, string reason, string? details)
    {
        var report = new Report
        {
            ReporterId = reporterId,
            TargetType = type,
            TopicId = type == ContentTargetType.Topic ? targetId : null,
            CommentId = type == ContentTargetType.Comment ? targetId : null,
            Reason = reason,
            Details = details,
            Status = ReportStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        _db.Reports.Add(report);
        await _db.SaveChangesAsync();
        return report.Id;
    }

    public async Task<bool> ResolveReportAsync(int reportId, int moderatorId, bool dismiss)
    {
        var r = await _db.Reports.FindAsync(reportId);
        if (r is null) return false;
        r.Status = dismiss ? ReportStatus.Dismissed : ReportStatus.Resolved;
        r.ResolvedById = moderatorId;
        r.ResolvedAt = DateTime.UtcNow;
        await LogAsync(moderatorId, dismiss ? ModerationAction.DismissReport : ModerationAction.ResolveReport,
            r.TargetType, r.TopicId ?? r.CommentId ?? 0, null);
        await _db.SaveChangesAsync();
        // Báo cho người đã gửi báo cáo.
        await _notifications.NotifyModerationAsync(r.ReporterId,
            dismiss ? "Báo cáo của bạn đã được xem xét và bỏ qua." : "Cảm ơn bạn — báo cáo của bạn đã được xử lý.",
            "/bang-tin");
        return true;
    }

    public async Task<bool> WarnUserAsync(int userId, int moderatorId, string reason)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user is null || string.IsNullOrWhiteSpace(reason)) return false;
        _db.UserWarnings.Add(new UserWarning { UserId = userId, ModeratorId = moderatorId, Reason = reason.Trim(), CreatedAt = DateTime.UtcNow });
        await LogAsync(moderatorId, ModerationAction.Warn, ContentTargetType.Topic, userId, user.DisplayName, reason.Trim());
        await _db.SaveChangesAsync();
        await _notifications.NotifyModerationAsync(userId, $"Bạn nhận một cảnh cáo từ ban quản trị: {reason.Trim()}", "/bang-tin");
        return true;
    }

    public async Task<DateTimeOffset?> SetMuteAsync(int userId, int moderatorId, int hours, string? reason)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user is null) return null;
        if (hours <= 0)
        {
            user.MutedUntil = null;
            await LogAsync(moderatorId, ModerationAction.Unmute, ContentTargetType.Topic, userId, user.DisplayName);
        }
        else
        {
            var until = DateTimeOffset.UtcNow.AddHours(hours);
            user.MutedUntil = until;
            await LogAsync(moderatorId, ModerationAction.Mute, ContentTargetType.Topic, userId, user.DisplayName,
                (reason ?? "").Trim() + $" ({hours}h)");
            await _notifications.NotifyModerationAsync(userId,
                $"Bạn bị tạm cấm đăng bài trong {hours} giờ." + (string.IsNullOrWhiteSpace(reason) ? "" : $" Lý do: {reason.Trim()}"),
                "/bang-tin");
        }
        await _db.SaveChangesAsync();
        return user.MutedUntil;
    }

    public async Task<bool> CanModerateCategoryAsync(int userId, bool isAdmin, int categoryId)
    {
        if (isAdmin) return true;
        var cats = await _db.CategoryModerators.Where(cm => cm.UserId == userId)
            .Select(cm => cm.CategoryId).ToListAsync();
        return cats.Count == 0 || cats.Contains(categoryId);
    }

    public Task<List<Report>> PendingReportsAsync()
        => _db.Reports
            .Include(r => r.Reporter)
            .Include(r => r.Topic)
            .Include(r => r.Comment).ThenInclude(c => c!.Topic)
            .Where(r => r.Status == ReportStatus.Pending)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

    public Task<List<ModerationLog>> AuditLogAsync(int take = 100)
        => _db.ModerationLogs
            .Include(m => m.Moderator)
            .OrderByDescending(m => m.CreatedAt)
            .Take(take)
            .ToListAsync();

    public async Task<PagedResult<ModerationLog>> AuditLogPagedAsync(string? keyword, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        IQueryable<ModerationLog> query = _db.ModerationLogs.Include(m => m.Moderator);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = $"%{keyword.Trim()}%";
            query = query.Where(m => EF.Functions.Like(m.TargetTitle!, kw)
                                     || EF.Functions.Like(m.Moderator.DisplayName, kw)
                                     || EF.Functions.Like(m.Detail!, kw));
        }
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PagedResult<ModerationLog> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    private Task LogAsync(int moderatorId, ModerationAction action, ContentTargetType type, int targetId,
        string? title, string? detail = null)
    {
        _db.ModerationLogs.Add(new ModerationLog
        {
            ModeratorId = moderatorId,
            Action = action,
            TargetType = type,
            TargetId = targetId,
            TargetTitle = title,
            Detail = detail,
            CreatedAt = DateTime.UtcNow
        });
        return Task.CompletedTask;
    }
}
