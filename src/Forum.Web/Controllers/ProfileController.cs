using Forum.Web.Helpers;
using Forum.Web.Models.ViewModels;
using Forum.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Forum.Web.Controllers;

public class ProfileController : ForumControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly ISearchService _search;
    private readonly INotificationService _notifications;
    private readonly ISeoService _seo;
    private readonly IForumUrlService _url;
    private readonly IWebHostEnvironment _env;

    public ProfileController(ApplicationDbContext db, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
        ISearchService search, INotificationService notifications, ISeoService seo, IForumUrlService url, IWebHostEnvironment env)
    {
        _db = db; _users = users; _signIn = signIn; _search = search; _notifications = notifications; _seo = seo; _url = url; _env = env;
    }

    // Tải ảnh đại diện (avatar) — thành viên đã đăng nhập. Chỉ nhận ảnh raster.
    [HttpPost("/cai-dat/tai-avatar")]
    [Authorize]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> UploadAvatar(IFormFile? file)
    {
        if (file is null || file.Length == 0) return BadRequest(new { message = "Chưa chọn tệp." });
        if (file.Length > 5 * 1024 * 1024) return BadRequest(new { message = "Ảnh vượt quá 5MB." });
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" }.Contains(ext))
            return BadRequest(new { message = "Định dạng ảnh không hỗ trợ." });
        var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var dir = Path.Combine(webRoot, "uploads");
        Directory.CreateDirectory(dir);
        var stored = $"avatar-{CurrentUserId}-{Guid.NewGuid():N}{ext}";
        await using (var fs = System.IO.File.Create(Path.Combine(dir, stored)))
            await file.CopyToAsync(fs);
        var url = $"/uploads/{stored}";
        // Lưu ngay vào hồ sơ + làm mới claim avatar.
        var user = await _users.GetUserAsync(User);
        if (user is not null) { user.AvatarUrl = url; await _users.UpdateAsync(user); await _signIn.RefreshSignInAsync(user); }
        return Json(new { url });
    }

    [HttpGet("/thanh-vien/{username}")]
    [AllowAnonymous]
    public async Task<IActionResult> Index(string username, string tab = "chu-de", int trang = 1)
    {
        var user = await _users.FindByNameAsync(username);
        if (user is null) return NotFound();

        var vm = new ProfileViewModel
        {
            User = user,
            Roles = await _users.GetRolesAsync(user),
            TopicCount = await _db.Topics.CountAsync(t => t.AuthorId == user.Id && !t.IsDeleted),
            CommentCount = await _db.Comments.CountAsync(c => c.AuthorId == user.Id && !c.IsDeleted),
            FollowerCount = await _db.UserFollows.CountAsync(f => f.FolloweeId == user.Id),
            FollowingCount = await _db.UserFollows.CountAsync(f => f.FollowerId == user.Id),
            IsSelf = IsAuthed && CurrentUserId == user.Id,
            IsFollowing = IsAuthed && await _db.UserFollows.AnyAsync(f => f.FollowerId == CurrentUserId && f.FolloweeId == user.Id),
            IsBlocked = IsAuthed && await _db.UserBlocks.AnyAsync(b => b.BlockerId == CurrentUserId && b.BlockedId == user.Id),
            Tab = tab
        };

        switch (tab)
        {
            case "binh-luan":
                vm.Comments = await _db.Comments.Include(c => c.Topic)
                    .Where(c => c.AuthorId == user.Id && !c.IsDeleted)
                    .OrderByDescending(c => c.CreatedAt).Take(30).ToListAsync();
                break;
            case "huy-hieu":
                vm.Badges = await _db.UserBadges.Include(b => b.Badge)
                    .Where(b => b.UserId == user.Id).OrderByDescending(b => b.AwardedAt).ToListAsync();
                break;
            default:
                vm.Topics = await _search.QueryAsync(new TopicQuery { AuthorId = user.Id, Sort = TopicSort.Latest, Page = trang, PageSize = 15 });
                break;
        }

        // Ghi chú nội bộ + công cụ nhân sự (chỉ mod/admin, không áp cho chính mình).
        if (IsAuthed && User.IsStaff() && CurrentUserId != user.Id)
            vm.Notes = await _db.UserNotes.Include(n => n.Author)
                .Where(n => n.UserId == user.Id).OrderByDescending(n => n.CreatedAt).ToListAsync();

        SetSeo(new SeoModel
        {
            Title = $"{user.DisplayName} (@{user.UserName})",
            Description = string.IsNullOrEmpty(user.Bio) ? $"Hồ sơ thành viên {user.DisplayName} trên Diễn đàn Xây dựng Việt." : user.Bio,
            CanonicalUrl = _url.Absolute(_url.User(user.UserName!)),
            OgType = "profile",
            JsonLd = { },
            Breadcrumbs = { new BreadcrumbItem("Trang chủ", "/"), new BreadcrumbItem("Thành viên", null), new BreadcrumbItem(user.DisplayName, null) }
        });
        ((SeoModel)ViewData["Seo"]!).JsonLd.Add(_seo.ProfileJsonLd(user));

        return View(vm);
    }

    [HttpGet("/thanh-vien/{username}/the")]
    [AllowAnonymous]
    public async Task<IActionResult> HoverCard(string username)
    {
        var user = await _users.FindByNameAsync(username);
        if (user is null) return NotFound();
        var roles = await _users.GetRolesAsync(user);
        var vm = new HoverCardViewModel
        {
            User = user,
            PrimaryRole = roles.Contains(Roles.Admin) ? Roles.Admin : roles.Contains(Roles.Moderator) ? Roles.Moderator : Roles.Member,
            BadgeCount = await _db.UserBadges.CountAsync(b => b.UserId == user.Id),
            TopicCount = await _db.Topics.CountAsync(t => t.AuthorId == user.Id && !t.IsDeleted),
            CommentCount = await _db.Comments.CountAsync(c => c.AuthorId == user.Id && !c.IsDeleted),
            IsAuthenticated = IsAuthed,
            IsSelf = IsAuthed && CurrentUserId == user.Id,
            IsFollowing = IsAuthed && await _db.UserFollows.AnyAsync(f => f.FollowerId == CurrentUserId && f.FolloweeId == user.Id)
        };
        return PartialView("_HoverCard", vm);
    }

    [HttpPost("/thanh-vien/{username}/theo-doi")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleFollow(string username)
    {
        var target = await _users.FindByNameAsync(username);
        if (target is null) return NotFound();
        if (target.Id == CurrentUserId) return BadRequest(new { message = "Không thể tự theo dõi chính mình." });

        var existing = await _db.UserFollows.FindAsync(CurrentUserId, target.Id);
        bool following;
        if (existing is null)
        {
            _db.UserFollows.Add(new UserFollow { FollowerId = CurrentUserId, FolloweeId = target.Id, CreatedAt = DateTime.UtcNow });
            await _db.SaveChangesAsync();
            await _notifications.NotifyFollowAsync(CurrentUserId, target.Id);
            following = true;
        }
        else
        {
            _db.UserFollows.Remove(existing);
            await _db.SaveChangesAsync();
            following = false;
        }
        var followers = await _db.UserFollows.CountAsync(f => f.FolloweeId == target.Id);
        return Json(new { following, followers });
    }

    [HttpGet("/cai-dat")]
    [Authorize]
    public async Task<IActionResult> Settings()
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return Challenge();
        SetSeo(new SeoModel { Title = "Cài đặt hồ sơ", NoIndex = true });
        ViewBag.Prefs = new[] { user.NotifyReplies, user.NotifyMentions, user.NotifyFollows, user.NotifyTagTopics };
        ViewBag.Blocked = await _db.UserBlocks.Where(b => b.BlockerId == user.Id)
            .Join(_db.Users, b => b.BlockedId, u => u.Id, (b, u) => new { u.UserName, u.DisplayName, u.Id })
            .ToListAsync();
        return View(new EditProfileViewModel { DisplayName = user.DisplayName, Bio = user.Bio, Location = user.Location, Trade = user.Trade, AvatarUrl = user.AvatarUrl });
    }

    [HttpPost("/cai-dat/thong-bao")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveNotificationPrefs(bool notifyReplies, bool notifyMentions, bool notifyFollows, bool notifyTagTopics)
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return Challenge();
        user.NotifyReplies = notifyReplies; user.NotifyMentions = notifyMentions;
        user.NotifyFollows = notifyFollows; user.NotifyTagTopics = notifyTagTopics;
        await _users.UpdateAsync(user);
        Toast("Đã lưu tùy chọn thông báo.");
        return RedirectToAction(nameof(Settings));
    }

    [HttpPost("/cai-dat")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Settings(EditProfileViewModel vm)
    {
        if (!ModelState.IsValid) { SetSeo(new SeoModel { Title = "Cài đặt hồ sơ", NoIndex = true }); return View(vm); }
        var user = await _users.GetUserAsync(User);
        if (user is null) return Challenge();

        user.DisplayName = vm.DisplayName.Trim();
        user.Bio = vm.Bio;
        user.Location = vm.Location;
        user.Trade = vm.Trade;
        user.AvatarUrl = string.IsNullOrWhiteSpace(vm.AvatarUrl) ? null : vm.AvatarUrl.Trim();
        await _users.UpdateAsync(user);
        await _signIn.RefreshSignInAsync(user); // cập nhật claim DisplayName/Avatar

        Toast("Đã lưu hồ sơ.");
        return RedirectToAction(nameof(Index), new { username = user.UserName });
    }

    [HttpGet("/ban-nhap")]
    [Authorize]
    public async Task<IActionResult> Drafts()
    {
        SetSeo(new SeoModel { Title = "Bản nháp của tôi", NoIndex = true });
        var drafts = await _db.Drafts.Where(d => d.UserId == CurrentUserId)
            .OrderByDescending(d => d.UpdatedAt).ToListAsync();
        return View(drafts);
    }

    [HttpPost("/ban-nhap/xoa")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDraft(int id)
    {
        var d = await _db.Drafts.FindAsync(id);
        if (d is not null && d.UserId == CurrentUserId) { _db.Drafts.Remove(d); await _db.SaveChangesAsync(); }
        Toast("Đã xóa bản nháp.");
        return RedirectToAction(nameof(Drafts));
    }

    // ---------------- GDPR: xuất dữ liệu + đóng tài khoản ----------------
    [HttpGet("/cai-dat/xuat-du-lieu")]
    [Authorize]
    public async Task<IActionResult> ExportData()
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return Challenge();
        var data = new
        {
            profile = new { user.UserName, user.DisplayName, user.Email, user.Bio, user.Location, user.Reputation, user.CreatedAt },
            topics = await _db.Topics.IgnoreQueryFilters().Where(t => t.AuthorId == user.Id)
                .Select(t => new { t.Title, t.Body, t.CreatedAt, t.Score }).ToListAsync(),
            comments = await _db.Comments.IgnoreQueryFilters().Where(c => c.AuthorId == user.Id)
                .Select(c => new { c.Body, c.CreatedAt, c.Score }).ToListAsync()
        };
        var json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", $"du-lieu-{user.UserName}.json");
    }

    [HttpPost("/cai-dat/dong-tai-khoan")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseAccount()
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return Challenge();
        // Ẩn danh + khóa vĩnh viễn + vô hiệu phiên. Giữ nội dung (đã ẩn danh tác giả).
        await _users.SetEmailAsync(user, $"closed-{user.Id}@deleted.local");
        user.DisplayName = "Tài khoản đã đóng";
        user.Bio = null; user.Location = null; user.AvatarUrl = null;
        await _users.SetLockoutEnabledAsync(user, true);
        await _users.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        await _users.UpdateAsync(user);
        await _users.UpdateSecurityStampAsync(user);
        await _signIn.SignOutAsync();
        Toast("Tài khoản của bạn đã được đóng. Cảm ơn bạn đã tham gia.");
        return RedirectToAction("Index", "Home");
    }

    [HttpGet("/bang-tin")]
    [Authorize]
    public async Task<IActionResult> Feed()
    {
        // Feed: hoạt động từ những người mình theo dõi + chủ đề mới ở box mình quan tâm.
        var followeeIds = await _db.UserFollows.Where(f => f.FollowerId == CurrentUserId).Select(f => f.FolloweeId).ToListAsync();
        var activities = await _db.UserActivities.Include(a => a.User).Include(a => a.Topic)
            .Where(a => followeeIds.Contains(a.UserId) || a.UserId == CurrentUserId)
            .OrderByDescending(a => a.CreatedAt).Take(40).ToListAsync();
        SetSeo(new SeoModel { Title = "Bảng tin của bạn", NoIndex = true });
        return View(activities);
    }

    [HttpGet("/da-luu")]
    [Authorize]
    public async Task<IActionResult> Bookmarks()
    {
        var orderedIds = await _db.Bookmarks.Where(b => b.UserId == CurrentUserId)
            .OrderByDescending(b => b.CreatedAt).Select(b => b.TopicId).ToListAsync();
        var loaded = await _db.Topics
            .Include(t => t.Author).Include(t => t.Category).Include(t => t.TopicTags).ThenInclude(tt => tt.Tag)
            .Where(t => orderedIds.Contains(t.Id) && !t.IsDeleted).ToListAsync();
        var topics = orderedIds.Select(id => loaded.FirstOrDefault(t => t.Id == id)).Where(t => t != null).Select(t => t!).ToList();
        SetSeo(new SeoModel { Title = "Chủ đề đã lưu", NoIndex = true });
        return View(topics);
    }
}
