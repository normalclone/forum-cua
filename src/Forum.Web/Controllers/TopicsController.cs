using Forum.Web.Helpers;
using Forum.Web.Models.ViewModels;
using Forum.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Forum.Web.Controllers;

public class TopicsController : ForumControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITopicService _topics;
    private readonly ICommentService _comments;
    private readonly IModerationService _moderation;
    private readonly ISeoService _seo;
    private readonly IForumUrlService _url;
    private readonly IMarkdownService _md;
    private readonly IWordFilterService _filter;

    private readonly ISiteSettingService _settings;
    private readonly IPostingGuardService _guard;
    private readonly IEngagementService _engagement;
    private readonly INotificationService _notifications;

    public TopicsController(ApplicationDbContext db, ITopicService topics, ICommentService comments,
        IModerationService moderation, ISeoService seo, IForumUrlService url, IMarkdownService md,
        IWordFilterService filter, ISiteSettingService settings, IPostingGuardService guard,
        IEngagementService engagement, INotificationService notifications)
    {
        _db = db; _topics = topics; _comments = comments; _moderation = moderation;
        _seo = seo; _url = url; _md = md; _filter = filter; _settings = settings;
        _guard = guard; _engagement = engagement; _notifications = notifications;
    }

    // ---------------- Detail ----------------
    [HttpGet("/chu-de/{id:int}/{slug?}")]
    [AllowAnonymous]
    public async Task<IActionResult> Detail(int id, string? slug, [FromQuery(Name = "binh-luan")] string commentSort = "top")
    {
        var topic = await _topics.GetForDetailAsync(id);
        if (topic is null || topic.IsDeleted) return NotFound();

        // Bài chờ kiểm duyệt: chỉ tác giả và nhân viên kiểm duyệt được xem.
        if (!topic.IsApproved && !(IsAuthed && (topic.AuthorId == CurrentUserId || User.IsStaff())))
            return NotFound();

        // 301 về canonical nếu slug sai (chuẩn SEO). So sánh với slug hiệu lực
        // (dùng fallback giống ForumUrlService) để tránh vòng lặp redirect khi slug rỗng.
        var canonicalSlug = string.IsNullOrEmpty(topic.Slug) ? "noi-dung" : topic.Slug;
        if (!string.Equals(slug, canonicalSlug, StringComparison.Ordinal))
            return RedirectPermanent(_url.Topic(topic));

        await _topics.IncrementViewAsync(id);

        var ordered = await _comments.GetThreadAsync(id);
        var roots = SortTree(BuildTree(ordered), commentSort);

        var vm = new TopicDetailViewModel
        {
            Topic = topic,
            CommentRoots = roots,
            CommentCount = ordered.Count(c => !c.IsDeleted),
            CommentSort = commentSort,
            CanEdit = IsAuthed && (topic.AuthorId == CurrentUserId || User.IsStaff()),
            // Mod chỉ thấy công cụ kiểm duyệt ở danh mục mình phụ trách (admin/mod chưa phân công: tất cả).
            CanModerate = User.IsStaff() && await _moderation.CanModerateCategoryAsync(CurrentUserId, User.IsInRole(Roles.Admin), topic.CategoryId),
            IsPendingApproval = !topic.IsApproved
        };

        if (IsAuthed)
        {
            var uid = CurrentUserId;
            vm.UserTopicVote = await _db.TopicVotes.Where(v => v.TopicId == id && v.UserId == uid).Select(v => (int)v.Value).FirstOrDefaultAsync();
            var cids = ordered.Select(c => c.Id).ToList();
            vm.UserCommentVotes = await _db.CommentVotes.Where(v => v.UserId == uid && cids.Contains(v.CommentId))
                .ToDictionaryAsync(v => v.CommentId, v => (int)v.Value);
            vm.IsBookmarked = await _db.Bookmarks.AnyAsync(b => b.UserId == uid && b.TopicId == id);
            vm.IsSubscribed = await _db.TopicSubscriptions.AnyAsync(s => s.UserId == uid && s.TopicId == id);
            if (topic.Poll != null)
                vm.UserPollOptionId = await _db.PollVotes.Where(p => p.PollId == topic.Poll.Id && p.UserId == uid)
                    .Select(p => (int?)p.PollOptionId).FirstOrDefaultAsync();
        }
        if (topic.Poll != null) vm.PollTotalVotes = topic.Poll.Options.Sum(o => o.VoteCount);

        // ---- Cảm xúc (emoji) + đáp án được chấp nhận ----
        vm.AllowedEmojis = _engagement.AllowedEmojis;
        var reactUid = IsAuthed ? CurrentUserId : 0;
        vm.TopicReactions = await _engagement.GetForTopicAsync(id, reactUid);
        vm.CommentReactions = await _engagement.GetForCommentsAsync(ordered.Select(c => c.Id).ToList(), reactUid);
        vm.CanAcceptAnswer = IsAuthed && topic.IsQuestion && (topic.AuthorId == CurrentUserId || vm.CanModerate);

        // ---- SEO ----
        var img = _md.FirstImageUrl(topic.Body);
        var seo = new SeoModel
        {
            Title = topic.Title,
            Description = _md.Excerpt(topic.Body, 160),
            CanonicalUrl = _url.Absolute(_url.Topic(topic)),
            OgType = "article",
            OgImage = img is not null ? _url.Absolute(img) : null,
            OgImageAlt = topic.Title,
            PublishedTime = topic.CreatedAt,
            ModifiedTime = topic.UpdatedAt ?? topic.LastActivityAt,
            AuthorName = topic.Author?.DisplayName,
            Breadcrumbs =
            {
                new BreadcrumbItem("Trang chủ", "/"),
                new BreadcrumbItem(topic.Category?.Name ?? "Danh mục", _url.Category(topic.Category?.Slug ?? "")),
                new BreadcrumbItem(topic.Title, null)
            }
        };
        var topComments = ordered.Where(c => !c.IsDeleted).OrderByDescending(c => c.Score).Take(15).ToList();
        seo.JsonLd.Add(_seo.DiscussionJsonLd(topic, topComments));
        SetSeo(seo);

        return View(vm);
    }

    // ---------------- Hover preview card ----------------
    // Thẻ xem nhanh nội dung chủ đề khi hover (giống thẻ hồ sơ /thanh-vien/{u}/the).
    // Tuyến chứa đoạn literal "the" nên được ưu tiên hơn tuyến slug {slug?} ở trên.
    [HttpGet("/chu-de/{id:int}/the")]
    [AllowAnonymous]
    public async Task<IActionResult> Card(int id)
    {
        var topic = await _db.Topics
            .Include(t => t.Author)
            .Include(t => t.Category)
            .Include(t => t.TopicTags).ThenInclude(tt => tt.Tag)
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
        if (topic is null) return NotFound();
        return PartialView("_TopicPreviewCard", topic);
    }

    // ---------------- Create ----------------
    [HttpGet("/tao-chu-de")]
    [Authorize]
    public async Task<IActionResult> Create([FromQuery(Name = "danh-muc")] string? danhMuc)
    {
        if (!_settings.GetBool(SettingKeys.FeaturePosting, true))
        {
            Toast("Chức năng tạo chủ đề đang tạm khóa.", "warning");
            return RedirectToAction("Index", "Home");
        }
        var cats = await _db.Categories.OrderBy(c => c.DisplayOrder).ToListAsync();
        var vm = new CreateTopicViewModel { Categories = cats };
        if (!string.IsNullOrEmpty(danhMuc))
            vm.CategoryId = cats.FirstOrDefault(c => c.Slug == danhMuc)?.Id ?? 0;
        SetSeo(new SeoModel { Title = "Tạo chủ đề mới", NoIndex = true });
        return View(vm);
    }

    [HttpPost("/tao-chu-de")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateTopicViewModel vm)
    {
        if (!_settings.GetBool(SettingKeys.FeaturePosting, true))
        {
            Toast("Chức năng tạo chủ đề đang tạm khóa.", "warning");
            return RedirectToAction("Index", "Home");
        }
        vm.Categories = await _db.Categories.OrderBy(c => c.DisplayOrder).ToListAsync();
        if (!await _db.Categories.AnyAsync(c => c.Id == vm.CategoryId))
            ModelState.AddModelError(nameof(vm.CategoryId), "Danh mục không hợp lệ.");
        if (_filter.ContainsBannedWord(vm.Title) || _filter.ContainsBannedWord(vm.Body))
            ModelState.AddModelError("", "Nội dung chứa từ ngữ không phù hợp, vui lòng chỉnh sửa.");
        // Nghi spam: nếu auto-mod bật thì KHÔNG chặn cứng mà đưa vào chờ duyệt (xử lý ở TopicService);
        // nếu tắt thì chặn như cũ.
        if (!_settings.GetBool(SettingKeys.AutomodSpam, true) && _filter.LooksLikeSpam(vm.Body))
            ModelState.AddModelError("", "Nội dung có dấu hiệu spam (quá nhiều liên kết/ký tự lặp).");

        var block = await _guard.CheckTopicAsync(CurrentUserId, User.IsStaff());
        if (block != null) ModelState.AddModelError("", block);

        if (!ModelState.IsValid) { SetSeo(new SeoModel { Title = "Tạo chủ đề mới", NoIndex = true }); return View(vm); }

        var tags = ParseTags(vm.Tags);
        var topic = await _topics.CreateAsync(CurrentUserId, vm.CategoryId, vm.Title, vm.Body, tags, vm.IsQuestion);

        // Poll (tuỳ chọn) — chỉ khi tính năng poll đang bật
        if (vm.AddPoll && !string.IsNullOrWhiteSpace(vm.PollQuestion) && _settings.GetBool(SettingKeys.FeaturePolls, true))
        {
            var options = (vm.PollOptionsText ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Take(8).ToList();
            if (options.Count >= 2)
            {
                var poll = new Poll { TopicId = topic.Id, Question = vm.PollQuestion.Trim(), CreatedAt = DateTime.UtcNow };
                var oi = 0;
                foreach (var o in options) poll.Options.Add(new PollOption { Text = o, DisplayOrder = oi++ });
                _db.Polls.Add(poll);
                await _db.SaveChangesAsync();
            }
        }

        // Thông báo cho người theo dõi thẻ (chỉ khi bài đã hiển thị; bài chờ duyệt sẽ báo khi được duyệt).
        if (topic.IsApproved)
            await _notifications.NotifyNewTopicToTagFollowersAsync(topic.Id, CurrentUserId);

        // Xoá nháp của người dùng (nếu có).
        var drafts = await _db.Drafts.Where(d => d.UserId == CurrentUserId).ToListAsync();
        if (drafts.Count > 0) { _db.Drafts.RemoveRange(drafts); await _db.SaveChangesAsync(); }

        Toast(topic.IsApproved
            ? "Đã đăng chủ đề!"
            : "Đã gửi bài. Chủ đề thuộc danh mục cần kiểm duyệt nên sẽ hiển thị sau khi được duyệt.",
            topic.IsApproved ? "success" : "warning");
        return Redirect(_url.Topic(topic));
    }

    // ---------------- Edit ----------------
    /// <summary>Tác giả sửa bài của mình; nhân sự chỉ sửa được bài trong danh mục mình phụ trách
    /// (đồng nhất với quyền kiểm duyệt theo danh mục).</summary>
    private async Task<bool> CanEditTopicAsync(Topic topic)
        => topic.AuthorId == CurrentUserId
           || (User.IsStaff() && await _moderation.CanModerateCategoryAsync(CurrentUserId, User.IsInRole(Roles.Admin), topic.CategoryId));

    [HttpGet("/chu-de/{id:int}/sua")]
    [Authorize]
    public async Task<IActionResult> Edit(int id)
    {
        var topic = await _db.Topics.Include(t => t.TopicTags).ThenInclude(tt => tt.Tag).FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
        if (topic is null) return NotFound();
        if (!await CanEditTopicAsync(topic)) return Forbid();

        SetSeo(new SeoModel { Title = "Chỉnh sửa chủ đề", NoIndex = true });
        return View(new EditTopicViewModel
        {
            Id = topic.Id, Title = topic.Title, Body = topic.Body,
            Tags = string.Join(", ", topic.TopicTags.Select(tt => tt.Tag.Name))
        });
    }

    [HttpPost("/chu-de/{id:int}/sua")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditTopicViewModel vm)
    {
        var topic = await _db.Topics.FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
        if (topic is null) return NotFound();
        if (!await CanEditTopicAsync(topic)) return Forbid();
        if (_filter.ContainsBannedWord(vm.Title) || _filter.ContainsBannedWord(vm.Body))
            ModelState.AddModelError("", "Nội dung chứa từ ngữ không phù hợp.");
        if (!ModelState.IsValid) { SetSeo(new SeoModel { Title = "Chỉnh sửa chủ đề", NoIndex = true }); return View(vm); }

        var updated = await _topics.UpdateAsync(id, CurrentUserId, vm.Title, vm.Body, ParseTags(vm.Tags));
        Toast("Đã cập nhật chủ đề.");
        return Redirect(_url.Topic(updated!));
    }

    // ---------------- Delete ----------------
    [HttpPost("/chu-de/{id:int}/xoa")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var topic = await _db.Topics.FirstOrDefaultAsync(t => t.Id == id);
        if (topic is null) return NotFound();

        bool ok;
        if (topic.AuthorId == CurrentUserId) ok = await _topics.SoftDeleteOwnAsync(id, CurrentUserId);
        else if (User.IsStaff() && await _moderation.CanModerateCategoryAsync(CurrentUserId, User.IsInRole(Roles.Admin), topic.CategoryId))
            ok = await _moderation.DeleteTopicAsync(id, CurrentUserId, "Xóa bởi kiểm duyệt");
        else return Forbid();

        if (IsAjax) return Json(new { ok });
        Toast(ok ? "Đã xóa chủ đề." : "Không thể xóa.", ok ? "success" : "error");
        return RedirectToAction("Index", "Home");
    }

    // ---------------- Bookmark / Subscribe ----------------
    [HttpPost("/chu-de/{id:int}/luu")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Bookmark(int id)
        => Json(new { bookmarked = await _topics.ToggleBookmarkAsync(id, CurrentUserId) });

    [HttpPost("/chu-de/{id:int}/theo-doi")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Subscribe(int id)
        => Json(new { subscribed = await _topics.ToggleSubscriptionAsync(id, CurrentUserId) });

    // ---------------- Report ----------------
    public record ReportRequest(string Type, int Id, string Reason, string? Details);

    [HttpPost("/bao-cao")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Report([FromBody] ReportRequest req)
    {
        var type = req.Type == "comment" ? ContentTargetType.Comment : ContentTargetType.Topic;
        await _moderation.CreateReportAsync(CurrentUserId, type, req.Id, req.Reason, req.Details);
        return Json(new { ok = true });
    }

    // ---------------- Draft autosave ----------------
    public record DraftRequest(int? CategoryId, string? Title, string? Body, string? Tags);

    [HttpPost("/nhap/luu")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveDraft([FromBody] DraftRequest req)
    {
        var draft = await _db.Drafts.FirstOrDefaultAsync(d => d.UserId == CurrentUserId)
                    ?? new Draft { UserId = CurrentUserId };
        if (draft.Id == 0) _db.Drafts.Add(draft);
        draft.CategoryId = req.CategoryId;
        draft.Title = req.Title;
        draft.Body = req.Body;
        draft.TagsCsv = req.Tags;
        draft.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Json(new { id = draft.Id, savedAt = draft.UpdatedAt });
    }

    [HttpGet("/nhap")]
    [Authorize]
    public async Task<IActionResult> GetDraft()
    {
        var d = await _db.Drafts.Where(x => x.UserId == CurrentUserId).OrderByDescending(x => x.UpdatedAt).FirstOrDefaultAsync();
        return Json(d is null ? null : new { d.Id, d.CategoryId, d.Title, d.Body, tags = d.TagsCsv, d.UpdatedAt });
    }

    // ---------------- helpers ----------------
    private static List<string> ParseTags(string? tags)
        => string.IsNullOrWhiteSpace(tags)
            ? new()
            : tags.Split(new[] { ',', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                  .Distinct(StringComparer.OrdinalIgnoreCase).Take(6).ToList();

    private static List<CommentNode> BuildTree(List<Comment> ordered)
    {
        var map = ordered.ToDictionary(c => c.Id, c => new CommentNode { Comment = c });
        var roots = new List<CommentNode>();
        foreach (var c in ordered)
        {
            var node = map[c.Id];
            if (c.ParentCommentId is int pid && map.TryGetValue(pid, out var parent))
                parent.Children.Add(node);
            else
                roots.Add(node);
        }
        return roots;
    }

    private static List<CommentNode> SortTree(List<CommentNode> nodes, string sort)
    {
        IEnumerable<CommentNode> sorted = sort switch
        {
            "moi" => nodes.OrderByDescending(n => n.Comment.CreatedAt),
            "cu" => nodes.OrderBy(n => n.Comment.CreatedAt),
            _ => nodes.OrderByDescending(n => n.Comment.Score).ThenBy(n => n.Comment.CreatedAt)
        };
        var list = sorted.ToList();
        foreach (var n in list) n.Children = SortTree(n.Children, sort);
        return list;
    }
}
