using Forum.Web.Helpers;
using Forum.Web.Hubs;
using Forum.Web.Models.ViewModels;
using Forum.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace Forum.Web.Controllers;

[Authorize]
public class ChatController : ForumControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IWebHostEnvironment _env;
    private readonly ISiteSettingService _settings;

    private static readonly string[] ChatImageExt = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    private static readonly string[] ChatFileExt = { ".pdf", ".doc", ".docx", ".xls", ".xlsx" };
    private const long NormalLimit = 1 * 1024 * 1024; // 1MB cho người dùng thường

    public ChatController(ApplicationDbContext db, UserManager<ApplicationUser> users, IWebHostEnvironment env,
        ISiteSettingService settings)
    {
        _db = db; _users = users; _env = env; _settings = settings;
    }

    // Chặn toàn bộ chat khi admin tắt tính năng.
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!_settings.GetBool(SettingKeys.FeatureChat, true))
            context.Result = NotFound();
        base.OnActionExecuting(context);
    }

    [HttpPost("/tin-nhan/tai-len")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(26 * 1024 * 1024)]
    public async Task<IActionResult> UploadJson(IFormFile? file)
    {
        if (file is null || file.Length == 0) return BadRequest(new { message = "Chưa chọn tệp." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var isImage = ChatImageExt.Contains(ext);
        if (!isImage && !ChatFileExt.Contains(ext))
            return BadRequest(new { message = "Chỉ hỗ trợ ảnh, PDF, Word (.docx), Excel (.xlsx)." });

        // Người dùng thường giới hạn 1MB; Admin/Mod không giới hạn (tới mức server cho phép).
        if (!User.IsStaff() && file.Length > NormalLimit)
            return BadRequest(new { message = "Tệp vượt quá 1MB. Chỉ Admin/Mod mới gửi được tệp lớn hơn." });

        var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var dir = Path.Combine(webRoot, "uploads");
        Directory.CreateDirectory(dir);
        var stored = $"{Guid.NewGuid():N}{ext}";
        await using (var fs = System.IO.File.Create(Path.Combine(dir, stored)))
            await file.CopyToAsync(fs);
        var url = $"/uploads/{stored}";

        _db.Attachments.Add(new Attachment
        {
            UploaderId = CurrentUserId,
            FileName = Path.GetFileName(file.FileName),
            StoredPath = url,
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            IsImage = isImage,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        return Json(new { url, name = Path.GetFileName(file.FileName), type = file.ContentType, isImage });
    }

    [HttpGet("/tin-nhan")]
    public async Task<IActionResult> Index() => await Open(null);

    [HttpGet("/tin-nhan/{id:int}")]
    public async Task<IActionResult> Conversation(int id) => await Open(id);

    [HttpGet("/tin-nhan/voi/{username}")]
    public async Task<IActionResult> With(string username)
    {
        var target = await _users.FindByNameAsync(username);
        if (target is null) return NotFound();
        if (target.Id == CurrentUserId) return RedirectToAction(nameof(Index));
        if (IsLocked(target)) { Toast("Không thể nhắn tin cho tài khoản đang bị khóa.", "warning"); return RedirectToAction(nameof(Index)); }
        if (await IsBlockedPairAsync(target.Id)) { Toast("Không thể nhắn tin do đã chặn giữa hai người.", "warning"); return RedirectToAction(nameof(Index)); }
        var convId = await FindOrCreateConversationAsync(target.Id);
        return RedirectToAction(nameof(Conversation), new { id = convId });
    }

    // ---------- JSON cho dock chat kiểu Messenger ----------

    [HttpGet("/tin-nhan/danh-sach")]
    public async Task<IActionResult> ListJson()
    {
        var uid = CurrentUserId;
        var convs = await _db.Conversations
            .Where(c => c.Participants.Any(p => p.UserId == uid))
            .Include(c => c.Participants).ThenInclude(p => p.User)
            .OrderByDescending(c => c.LastMessageAt).Take(25).ToListAsync();

        var items = new List<object>();
        foreach (var c in convs)
        {
            var other = c.Participants.FirstOrDefault(p => p.UserId != uid)?.User;
            if (other is null) continue;
            var last = await _db.ChatMessages.Where(m => m.ConversationId == c.Id)
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new { m.Body, m.SenderId, m.IsImageAttachment, HasAttach = m.AttachmentUrl != null })
                .FirstOrDefaultAsync();
            var myPart = c.Participants.First(p => p.UserId == uid);
            var unread = await _db.ChatMessages.AnyAsync(m => m.ConversationId == c.Id && m.SenderId != uid
                && (myPart.LastReadAt == null || m.CreatedAt > myPart.LastReadAt));
            string? lastText = last == null ? null
                : !string.IsNullOrEmpty(last.Body) ? last.Body
                : last.HasAttach ? (last.IsImageAttachment ? "[Hình ảnh]" : "[Tệp đính kèm]") : null;
            items.Add(new
            {
                id = c.Id, otherId = other.Id, name = other.DisplayName, username = other.UserName,
                avatar = other.AvatarUrl, lastMessage = lastText, lastMine = last != null && last.SenderId == uid,
                online = ChatHub.IsOnline(other.Id), unread
            });
        }
        return Json(items);
    }

    [HttpGet("/tin-nhan/{id:int}/tin")]
    public async Task<IActionResult> MessagesJson(int id)
    {
        var uid = CurrentUserId;
        var conv = await _db.Conversations.Include(c => c.Participants).ThenInclude(p => p.User)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (conv is null || conv.Participants.All(p => p.UserId != uid)) return NotFound();
        var other = conv.Participants.FirstOrDefault(p => p.UserId != uid)?.User;

        var messages = await _db.ChatMessages.Where(m => m.ConversationId == id)
            .OrderBy(m => m.CreatedAt).Take(100)
            .Select(m => new
            {
                m.Id, m.SenderId, m.Body, m.CreatedAt,
                attachmentUrl = m.AttachmentUrl, attachmentName = m.AttachmentName,
                attachmentType = m.AttachmentType, isImage = m.IsImageAttachment
            }).ToListAsync();

        var part = conv.Participants.First(p => p.UserId == uid);
        part.LastReadAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Json(new
        {
            id, me = uid, otherId = other?.Id, name = other?.DisplayName, username = other?.UserName,
            avatar = other?.AvatarUrl, online = other != null && ChatHub.IsOnline(other.Id), messages
        });
    }

    [HttpPost("/tin-nhan/voi-nguoi/{username}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OpenWithJson(string username)
    {
        var target = await _users.FindByNameAsync(username);
        if (target is null) return NotFound();
        if (target.Id == CurrentUserId) return BadRequest(new { message = "Không thể tự nhắn cho chính mình." });
        if (IsLocked(target)) return BadRequest(new { message = "Không thể nhắn tin cho tài khoản đang bị khóa." });
        if (await IsBlockedPairAsync(target.Id)) return BadRequest(new { message = "Không thể nhắn tin do đã chặn giữa hai người." });
        var convId = await FindOrCreateConversationAsync(target.Id);
        return Json(new
        {
            conversationId = convId, otherId = target.Id, name = target.DisplayName,
            username = target.UserName, avatar = target.AvatarUrl, online = ChatHub.IsOnline(target.Id)
        });
    }

    private static bool IsLocked(ApplicationUser u) => u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow;

    private Task<bool> IsBlockedPairAsync(int otherId)
        => _db.UserBlocks.AnyAsync(b => (b.BlockerId == CurrentUserId && b.BlockedId == otherId)
                                     || (b.BlockerId == otherId && b.BlockedId == CurrentUserId));

    private async Task<int> FindOrCreateConversationAsync(int otherUserId)
    {
        var existing = await _db.Conversations
            .Where(c => c.Participants.Count == 2
                        && c.Participants.Any(p => p.UserId == CurrentUserId)
                        && c.Participants.Any(p => p.UserId == otherUserId))
            .Select(c => c.Id).FirstOrDefaultAsync();
        if (existing != 0) return existing;

        var now = DateTime.UtcNow;
        var conv = new Conversation { CreatedAt = now, LastMessageAt = now };
        conv.Participants.Add(new ConversationParticipant { UserId = CurrentUserId });
        conv.Participants.Add(new ConversationParticipant { UserId = otherUserId });
        _db.Conversations.Add(conv);
        await _db.SaveChangesAsync();
        return conv.Id;
    }

    private async Task<IActionResult> Open(int? id)
    {
        var uid = CurrentUserId;
        SetSeo(new SeoModel { Title = "Tin nhắn", NoIndex = true });

        var convs = await _db.Conversations
            .Where(c => c.Participants.Any(p => p.UserId == uid))
            .Include(c => c.Participants).ThenInclude(p => p.User)
            .OrderByDescending(c => c.LastMessageAt)
            .ToListAsync();

        var summaries = new List<ConversationSummary>();
        foreach (var c in convs)
        {
            var other = c.Participants.FirstOrDefault(p => p.UserId != uid)?.User;
            if (other is null) continue;
            var last = await _db.ChatMessages.Where(m => m.ConversationId == c.Id)
                .OrderByDescending(m => m.CreatedAt).Select(m => m.Body).FirstOrDefaultAsync();
            var myPart = c.Participants.First(p => p.UserId == uid);
            var unread = await _db.ChatMessages.AnyAsync(m => m.ConversationId == c.Id && m.SenderId != uid
                && (myPart.LastReadAt == null || m.CreatedAt > myPart.LastReadAt));
            summaries.Add(new ConversationSummary(c.Id, other, last, c.LastMessageAt, ChatHub.IsOnline(other.Id), unread));
        }

        var vm = new ChatViewModel { Conversations = summaries, CurrentUserId = uid };

        if (id.HasValue)
        {
            var active = convs.FirstOrDefault(c => c.Id == id.Value);
            if (active is null) return NotFound();
            vm.Active = active;
            vm.Other = active.Participants.FirstOrDefault(p => p.UserId != uid)?.User;
            vm.OtherOnline = vm.Other != null && ChatHub.IsOnline(vm.Other.Id);
            vm.Messages = await _db.ChatMessages.Where(m => m.ConversationId == id.Value)
                .Include(m => m.Sender).OrderBy(m => m.CreatedAt).Take(200).ToListAsync();

            // Đánh dấu đã đọc.
            var part = active.Participants.First(p => p.UserId == uid);
            part.LastReadAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return View("Index", vm);
    }
}
