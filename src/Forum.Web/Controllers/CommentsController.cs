using Forum.Web.Helpers;
using Forum.Web.Models.ViewModels;
using Forum.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Forum.Web.Controllers;

[Route("binh-luan")]
public class CommentsController : ForumControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICommentService _comments;
    private readonly IModerationService _moderation;
    private readonly IMarkdownService _md;
    private readonly IWordFilterService _filter;
    private readonly ISiteSettingService _settings;
    private readonly IPostingGuardService _guard;
    private readonly IEngagementService _engagement;

    public CommentsController(ApplicationDbContext db, ICommentService comments, IModerationService moderation,
        IMarkdownService md, IWordFilterService filter, ISiteSettingService settings, IPostingGuardService guard,
        IEngagementService engagement)
    {
        _db = db; _comments = comments; _moderation = moderation; _md = md; _filter = filter;
        _settings = settings; _guard = guard; _engagement = engagement;
    }

    private bool PostingOpen => _settings.GetBool(SettingKeys.FeaturePosting, true);

    public record AddRequest(int TopicId, int? ParentId, string Body);
    public record EditRequest(string Body);

    [HttpPost("them")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add([FromBody] AddRequest req)
    {
        if (!PostingOpen)
            return BadRequest(new { message = "Diễn đàn đang ở chế độ chỉ đọc, tạm thời không thể bình luận." });
        var block = await _guard.CheckCommentAsync(CurrentUserId, User.IsStaff());
        if (block != null) return BadRequest(new { message = block });
        if (string.IsNullOrWhiteSpace(req.Body) || req.Body.Trim().Length < 2)
            return BadRequest(new { message = "Nội dung bình luận quá ngắn." });
        if (_filter.ContainsBannedWord(req.Body))
            return BadRequest(new { message = "Bình luận chứa từ ngữ không phù hợp." });

        Comment comment;
        try
        {
            comment = await _comments.AddAsync(req.TopicId, CurrentUserId, req.ParentId, req.Body.Trim());
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        comment.Author = (await _db.Users.FindAsync(CurrentUserId))!;
        var topic = await _db.Topics.Where(t => t.Id == req.TopicId)
            .Select(t => new { t.IsQuestion, t.AuthorId, t.CategoryId, t.AcceptedAnswerId }).FirstOrDefaultAsync();
        var canAccept = topic != null && topic.IsQuestion &&
            (topic.AuthorId == CurrentUserId ||
             (User.IsStaff() && await _moderation.CanModerateCategoryAsync(CurrentUserId, User.IsInRole(Roles.Admin), topic.CategoryId)));
        var render = new CommentRenderModel
        {
            Node = new CommentNode { Comment = comment },
            UserVotes = new(),
            CurrentUserId = CurrentUserId,
            CanModerate = User.IsStaff(),
            TopicLocked = false,
            TopicId = req.TopicId,
            IsQuestion = topic?.IsQuestion ?? false,
            AcceptedAnswerId = topic?.AcceptedAnswerId,
            CanAccept = canAccept,
            AllowedEmojis = _engagement.AllowedEmojis
        };
        var html = await this.RenderViewToStringAsync("_Comment", render);
        var count = await _db.Comments.CountAsync(c => c.TopicId == req.TopicId && !c.IsDeleted);
        return Json(new { html, count, parentId = req.ParentId });
    }

    [HttpPost("{id:int}/sua")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [FromBody] EditRequest req)
    {
        if (!PostingOpen)
            return BadRequest(new { message = "Diễn đàn đang ở chế độ chỉ đọc, tạm thời không thể sửa bình luận." });
        if (string.IsNullOrWhiteSpace(req.Body)) return BadRequest(new { message = "Nội dung trống." });
        if (_filter.ContainsBannedWord(req.Body)) return BadRequest(new { message = "Chứa từ ngữ không phù hợp." });

        var updated = await _comments.UpdateOwnAsync(id, CurrentUserId, req.Body.Trim());
        if (updated is null) return Forbid();
        return Json(new { html = _md.ToHtml(updated.Body), edited = true });
    }

    [HttpPost("{id:int}/xoa")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var comment = await _db.Comments.FirstOrDefaultAsync(c => c.Id == id);
        if (comment is null) return NotFound();

        bool ok;
        if (comment.AuthorId == CurrentUserId) ok = await _comments.SoftDeleteOwnAsync(id, CurrentUserId);
        else if (User.IsStaff())
        {
            var catId = await _db.Topics.IgnoreQueryFilters().Where(t => t.Id == comment.TopicId)
                .Select(t => t.CategoryId).FirstOrDefaultAsync();
            if (!await _moderation.CanModerateCategoryAsync(CurrentUserId, User.IsInRole(Roles.Admin), catId)) return Forbid();
            ok = await _moderation.DeleteCommentAsync(id, CurrentUserId, "Xóa bởi kiểm duyệt");
        }
        else return Forbid();

        return Json(new { ok });
    }
}
