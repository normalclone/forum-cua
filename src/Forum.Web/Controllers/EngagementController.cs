using Forum.Web.Helpers;
using Forum.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Forum.Web.Controllers;

/// <summary>Tương tác: thả cảm xúc, theo dõi thẻ, chọn đáp án hay.</summary>
[Route("tuong-tac")]
[Authorize]
public class EngagementController : ForumControllerBase
{
    private readonly IEngagementService _engagement;
    private readonly IModerationService _moderation;
    private readonly ApplicationDbContext _db;

    public EngagementController(IEngagementService engagement, IModerationService moderation, ApplicationDbContext db)
    {
        _engagement = engagement; _moderation = moderation; _db = db;
    }

    public record ReactRequest(bool IsComment, int Id, string Emoji);

    [HttpPost("cam-xuc")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> React([FromBody] ReactRequest req)
    {
        try
        {
            var s = await _engagement.ToggleReactionAsync(CurrentUserId, req.IsComment, req.Id, req.Emoji);
            return Json(new { counts = s.Counts, mine = s.Mine });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    public record TagFollowRequest(int TagId);

    [HttpPost("the/theo-doi")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FollowTag([FromBody] TagFollowRequest req)
    {
        try
        {
            var on = await _engagement.ToggleTagSubscriptionAsync(CurrentUserId, req.TagId);
            return Json(new { subscribed = on });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    public record AcceptRequest(int TopicId, int CommentId);

    [HttpPost("dap-an")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptAnswer([FromBody] AcceptRequest req)
    {
        var topic = await _db.Topics.Where(t => t.Id == req.TopicId)
            .Select(t => new { t.AuthorId, t.CategoryId, t.IsQuestion }).FirstOrDefaultAsync();
        if (topic is null) return NotFound();
        if (!topic.IsQuestion) return BadRequest(new { message = "Chỉ chủ đề Hỏi–Đáp mới chọn được đáp án." });

        var allowed = topic.AuthorId == CurrentUserId
            || (User.IsStaff() && await _moderation.CanModerateCategoryAsync(CurrentUserId, User.IsInRole(Roles.Admin), topic.CategoryId));
        if (!allowed) return Forbid();

        var (ok, acceptedId) = await _engagement.ToggleAcceptedAnswerAsync(req.TopicId, req.CommentId, CurrentUserId);
        return Json(new { ok, acceptedId });
    }
}
