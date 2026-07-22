using Forum.Web.Models;
using Forum.Web.Models.ViewModels;
using Forum.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Forum.Web.Controllers;

[Authorize(Roles = Roles.Staff)]
[Route("kiem-duyet")]
public class ModerationController : ForumControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IModerationService _moderation;
    private readonly UserManager<ApplicationUser> _users;

    public ModerationController(ApplicationDbContext db, IModerationService moderation, UserManager<ApplicationUser> users)
    {
        _db = db; _moderation = moderation; _users = users;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        SetSeo(new SeoModel { Title = "Bảng kiểm duyệt", NoIndex = true });
        var pendingTopics = await _moderation.PendingTopicsAsync();
        var reports = await _moderation.PendingReportsAsync();

        // Mod được phân công danh mục → chỉ thấy hàng chờ duyệt + báo cáo của danh mục mình phụ trách.
        var myCats = await MyCategoriesAsync();
        if (myCats != null)
        {
            pendingTopics = pendingTopics.Where(t => myCats.Contains(t.CategoryId)).ToList();
            reports = reports.Where(r =>
            {
                var catId = r.Topic?.CategoryId ?? r.Comment?.Topic?.CategoryId;
                return catId == null || myCats.Contains(catId.Value);
            }).ToList();
        }

        return View(new ModerationViewModel
        {
            PendingReports = reports,
            PendingTopics = pendingTopics,
            AuditLog = await _moderation.AuditLogAsync(50),
            TotalTopics = await _db.Topics.CountAsync(t => !t.IsDeleted),
            TotalComments = await _db.Comments.CountAsync(c => !c.IsDeleted),
            PendingReportCount = reports.Count,
            PendingApprovalCount = pendingTopics.Count
        });
    }

    /// <summary>Danh mục mà mod hiện tại phụ trách; null nghĩa là "tất cả" (admin, hoặc mod chưa được phân công).</summary>
    private async Task<List<int>?> MyCategoriesAsync()
    {
        if (User.IsInRole(Roles.Admin)) return null;
        var cats = await _db.CategoryModerators.Where(cm => cm.UserId == CurrentUserId).Select(cm => cm.CategoryId).ToListAsync();
        return cats.Count == 0 ? null : cats;
    }

    private async Task<bool> CanModerateTopicAsync(int topicId)
    {
        var myCats = await MyCategoriesAsync();
        if (myCats == null) return true;
        var catId = await _db.Topics.IgnoreQueryFilters().Where(t => t.Id == topicId).Select(t => t.CategoryId).FirstOrDefaultAsync();
        return myCats.Contains(catId);
    }

    private async Task<bool> CanModerateCommentAsync(int commentId)
    {
        var myCats = await MyCategoriesAsync();
        if (myCats == null) return true;
        var catId = await _db.Comments.IgnoreQueryFilters().Where(c => c.Id == commentId)
            .Select(c => (int?)c.Topic.CategoryId).FirstOrDefaultAsync();
        return catId != null && myCats.Contains(catId.Value);
    }

    private async Task<bool> CanModerateReportAsync(int reportId)
    {
        var myCats = await MyCategoriesAsync();
        if (myCats == null) return true;
        var r = await _db.Reports.FirstOrDefaultAsync(x => x.Id == reportId);
        if (r == null) return true;   // để service trả về not-found
        int? catId = r.TopicId != null
            ? await _db.Topics.IgnoreQueryFilters().Where(t => t.Id == r.TopicId).Select(t => (int?)t.CategoryId).FirstOrDefaultAsync()
            : r.CommentId != null
                ? await _db.Comments.IgnoreQueryFilters().Where(c => c.Id == r.CommentId).Select(c => (int?)c.Topic.CategoryId).FirstOrDefaultAsync()
                : null;
        return catId == null || myCats.Contains(catId.Value);
    }

    private static IActionResult NotYourCategory() => new JsonResult(new { ok = false, error = "Bạn không phụ trách danh mục này." });

    [HttpGet("nhat-ky")]
    public async Task<IActionResult> AuditLog([FromQuery(Name = "q")] string? q, [FromQuery(Name = "trang")] int trang = 1)
    {
        SetSeo(new SeoModel { Title = "Nhật ký kiểm duyệt", NoIndex = true });
        ViewData["q"] = q;
        return View(await _moderation.AuditLogPagedAsync(q, trang, 30));
    }

    public record ApproveRequest(int Id, string? Reason);

    [HttpPost("duyet")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve([FromBody] ApproveRequest req)
    {
        if (!await CanModerateTopicAsync(req.Id)) return Json(new { ok = false, error = "Bạn không phụ trách danh mục này." });
        return Json(new { ok = await _moderation.ApproveTopicAsync(req.Id, CurrentUserId) });
    }

    [HttpPost("tu-choi")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject([FromBody] ApproveRequest req)
    {
        if (!await CanModerateTopicAsync(req.Id)) return Json(new { ok = false, error = "Bạn không phụ trách danh mục này." });
        return Json(new { ok = await _moderation.RejectTopicAsync(req.Id, CurrentUserId, req.Reason) });
    }

    public record ModRequest(int Id, bool On);

    [HttpPost("ghim")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pin([FromBody] ModRequest req)
        => !await CanModerateTopicAsync(req.Id) ? NotYourCategory()
            : Json(new { ok = await _moderation.SetPinnedAsync(req.Id, CurrentUserId, req.On) });

    [HttpPost("khoa")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Lock([FromBody] ModRequest req)
        => !await CanModerateTopicAsync(req.Id) ? NotYourCategory()
            : Json(new { ok = await _moderation.SetLockedAsync(req.Id, CurrentUserId, req.On) });

    [HttpPost("noi-bat")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Feature([FromBody] ModRequest req)
        => !await CanModerateTopicAsync(req.Id) ? NotYourCategory()
            : Json(new { ok = await _moderation.SetFeaturedAsync(req.Id, CurrentUserId, req.On) });

    public record MoveRequest(int Id, int CategoryId);

    [HttpPost("di-chuyen")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Move([FromBody] MoveRequest req)
    {
        // Phải phụ trách CẢ danh mục nguồn lẫn danh mục đích.
        if (!await CanModerateTopicAsync(req.Id)
            || !await _moderation.CanModerateCategoryAsync(CurrentUserId, User.IsInRole(Roles.Admin), req.CategoryId))
            return NotYourCategory();
        return Json(new { ok = await _moderation.MoveAsync(req.Id, CurrentUserId, req.CategoryId) });
    }

    public record ResolveRequest(int Id, bool Dismiss);

    [HttpPost("bao-cao/giai-quyet")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve([FromBody] ResolveRequest req)
        => !await CanModerateReportAsync(req.Id) ? NotYourCategory()
            : Json(new { ok = await _moderation.ResolveReportAsync(req.Id, CurrentUserId, req.Dismiss) });

    // Xóa nội dung bị báo cáo trực tiếp từ bảng kiểm duyệt.
    public record DeleteRequest(string Type, int Id);

    [HttpPost("xoa")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteContent([FromBody] DeleteRequest req)
    {
        var allowed = req.Type == "comment"
            ? await CanModerateCommentAsync(req.Id)
            : await CanModerateTopicAsync(req.Id);
        if (!allowed) return NotYourCategory();

        var ok = req.Type == "comment"
            ? await _moderation.DeleteCommentAsync(req.Id, CurrentUserId, "Xóa từ bảng kiểm duyệt")
            : await _moderation.DeleteTopicAsync(req.Id, CurrentUserId, "Xóa từ bảng kiểm duyệt");
        return Json(new { ok });
    }

    // ---------------- Cảnh cáo / tạm cấm (mod + admin) ----------------
    private async Task<bool> IsStaffUserAsync(int userId)
    {
        var u = await _users.FindByIdAsync(userId.ToString());
        return u != null && (await _users.IsInRoleAsync(u, Roles.Admin) || await _users.IsInRoleAsync(u, Roles.Moderator));
    }

    public record WarnRequest(int Id, string Reason);

    [HttpPost("canh-cao")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Warn([FromBody] WarnRequest req)
    {
        if (req.Id == CurrentUserId || await IsStaffUserAsync(req.Id)) return Json(new { ok = false, error = "Không thể cảnh cáo tài khoản này." });
        var ok = await _moderation.WarnUserAsync(req.Id, CurrentUserId, req.Reason ?? "");
        return Json(new { ok, error = ok ? null : "Nhập lý do cảnh cáo." });
    }

    public record MuteRequest(int Id, int Hours, string? Reason);

    [HttpPost("tam-cam")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Mute([FromBody] MuteRequest req)
    {
        if (req.Id == CurrentUserId || await IsStaffUserAsync(req.Id)) return Json(new { ok = false, error = "Không thể tạm cấm tài khoản này." });
        var until = await _moderation.SetMuteAsync(req.Id, CurrentUserId, req.Hours, req.Reason);
        return Json(new { ok = true, mutedUntil = until });
    }

    // ---------------- Thao tác hàng loạt ----------------
    public record BulkRequest(int[] Ids, string? Reason);

    [HttpPost("duyet-nhieu")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkApprove([FromBody] BulkRequest req)
    {
        var n = 0;
        foreach (var id in req.Ids ?? Array.Empty<int>())
            if (await CanModerateTopicAsync(id) && await _moderation.ApproveTopicAsync(id, CurrentUserId)) n++;
        return Json(new { ok = true, count = n });
    }

    [HttpPost("tu-choi-nhieu")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkReject([FromBody] BulkRequest req)
    {
        var n = 0;
        foreach (var id in req.Ids ?? Array.Empty<int>())
            if (await CanModerateTopicAsync(id) && await _moderation.RejectTopicAsync(id, CurrentUserId, req.Reason)) n++;
        return Json(new { ok = true, count = n });
    }

    // ---------------- Ghi chú nội bộ về thành viên ----------------
    public record NoteRequest(int UserId, string Body);

    [HttpPost("ghi-chu")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddNote([FromBody] NoteRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Body)) return Json(new { ok = false, error = "Nhập nội dung ghi chú." });
        _db.UserNotes.Add(new UserNote { UserId = req.UserId, AuthorId = CurrentUserId, Body = req.Body.Trim(), CreatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();
        return Json(new { ok = true });
    }

    public record NoteDeleteRequest(int Id);

    [HttpPost("ghi-chu/xoa")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteNote([FromBody] NoteDeleteRequest req)
    {
        var note = await _db.UserNotes.FindAsync(req.Id);
        if (note != null) { _db.UserNotes.Remove(note); await _db.SaveChangesAsync(); }
        return Json(new { ok = true });
    }
}
