using System.Text;
using Forum.Web.Helpers;
using Forum.Web.Models.ViewModels;
using Forum.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Forum.Web.Controllers;

/// <summary>Khu vực quản trị (chỉ Admin): tổng quan, người dùng, danh mục, thẻ, cấu hình.</summary>
[Authorize(Roles = Roles.Admin)]
[Route("quan-tri")]
public class AdminController : ForumControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ISlugService _slug;
    private readonly ISiteSettingService _settings;
    private readonly IModerationService _moderation;
    private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _cache;
    private readonly IWebHostEnvironment _env;
    private readonly INotificationService _notifications;

    public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> users,
        ISlugService slug, ISiteSettingService settings, IModerationService moderation,
        Microsoft.Extensions.Caching.Memory.IMemoryCache cache, IWebHostEnvironment env,
        INotificationService notifications)
    {
        _db = db; _users = users; _slug = slug; _settings = settings; _moderation = moderation;
        _cache = cache; _env = env; _notifications = notifications;
    }

    // ---------------- Dashboard ----------------
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        SetSeo(new SeoModel { Title = "Quản trị", NoIndex = true });
        var today = DateTime.UtcNow.Date;
        var now = DateTimeOffset.UtcNow;
        // SQLite không dịch được so sánh DateTimeOffset trong SQL → nạp các mốc khóa rồi đếm trong bộ nhớ.
        var lockEnds = await _db.Users.Where(u => u.LockoutEnd != null).Select(u => u.LockoutEnd).ToListAsync();
        var vm = new AdminDashboardViewModel
        {
            UserCount = await _db.Users.CountAsync(),
            LockedUserCount = lockEnds.Count(e => e > now),
            TopicCount = await _db.Topics.CountAsync(t => !t.IsDeleted),
            CommentCount = await _db.Comments.CountAsync(c => !c.IsDeleted),
            CategoryCount = await _db.Categories.CountAsync(),
            TagCount = await _db.Tags.CountAsync(),
            PendingReportCount = await _db.Reports.CountAsync(r => r.Status == ReportStatus.Pending),
            TodayTopics = await _db.Topics.CountAsync(t => !t.IsDeleted && t.CreatedAt >= today),
            TodayComments = await _db.Comments.CountAsync(c => !c.IsDeleted && c.CreatedAt >= today),
            NewestUsers = await _db.Users.OrderByDescending(u => u.CreatedAt).Take(6).ToListAsync()
        };
        var cats = await _db.Categories
            .Select(c => new { c.Name, c.ColorHex, Count = c.Topics.Count(t => !t.IsDeleted) })
            .ToListAsync();
        vm.TopCategories = cats.OrderByDescending(x => x.Count).Take(6)
            .Select(x => (x.Name, x.ColorHex, x.Count)).ToList();

        // Hoạt động 30 ngày gần nhất (gộp theo ngày trong bộ nhớ — SQLite không dịch .Date).
        var cutoff = DateTime.UtcNow.Date.AddDays(-29);
        var tDays = await _db.Topics.Where(t => !t.IsDeleted && t.CreatedAt >= cutoff).Select(t => t.CreatedAt).ToListAsync();
        var cDays = await _db.Comments.Where(c => !c.IsDeleted && c.CreatedAt >= cutoff).Select(c => c.CreatedAt).ToListAsync();
        var uDays = await _db.Users.Where(u => u.CreatedAt >= cutoff).Select(u => u.CreatedAt).ToListAsync();
        var tg = tDays.GroupBy(d => d.Date).ToDictionary(g => g.Key, g => g.Count());
        var cg = cDays.GroupBy(d => d.Date).ToDictionary(g => g.Key, g => g.Count());
        var ug = uDays.GroupBy(d => d.Date).ToDictionary(g => g.Key, g => g.Count());
        vm.Activity = Enumerable.Range(0, 30).Select(i =>
        {
            var day = cutoff.AddDays(i);
            return new ActivityPoint(day, tg.GetValueOrDefault(day), cg.GetValueOrDefault(day), ug.GetValueOrDefault(day));
        }).ToList();
        return View(vm);
    }

    // ---------------- Users ----------------
    [HttpGet("nguoi-dung")]
    public async Task<IActionResult> Users([FromQuery(Name = "q")] string? q,
        [FromQuery(Name = "vai-tro")] string? role, [FromQuery(Name = "trang")] int trang = 1)
    {
        SetSeo(new SeoModel { Title = "Quản lý người dùng", NoIndex = true });
        const int pageSize = 20;
        var page = Math.Max(1, trang);

        IQueryable<ApplicationUser> query = _db.Users;
        if (!string.IsNullOrWhiteSpace(q))
        {
            var kw = $"%{q.Trim()}%";
            query = query.Where(u => EF.Functions.Like(u.DisplayName, kw)
                                     || EF.Functions.Like(u.UserName!, kw)
                                     || EF.Functions.Like(u.Email!, kw));
        }
        if (!string.IsNullOrWhiteSpace(role))
        {
            var uidsInRole = from ur in _db.UserRoles
                             join r in _db.Roles on ur.RoleId equals r.Id
                             where r.Name == role
                             select ur.UserId;
            query = query.Where(u => uidsInRole.Contains(u.Id));
        }

        var total = await query.CountAsync();
        var users = await query.OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var ids = users.Select(u => u.Id).ToList();
        var roleByUser = await (from ur in _db.UserRoles
                                join r in _db.Roles on ur.RoleId equals r.Id
                                where ids.Contains(ur.UserId)
                                select new { ur.UserId, r.Name })
            .ToListAsync();
        var topicCounts = await _db.Topics.Where(t => ids.Contains(t.AuthorId) && !t.IsDeleted)
            .GroupBy(t => t.AuthorId).Select(g => new { g.Key, C = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.C);
        var commentCounts = await _db.Comments.Where(c => ids.Contains(c.AuthorId) && !c.IsDeleted)
            .GroupBy(c => c.AuthorId).Select(g => new { g.Key, C = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.C);

        var rows = users.Select(u => new AdminUserRow
        {
            User = u,
            Role = roleByUser.FirstOrDefault(x => x.UserId == u.Id)?.Name ?? Roles.Member,
            IsLocked = u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow,
            TopicCount = topicCounts.GetValueOrDefault(u.Id),
            CommentCount = commentCounts.GetValueOrDefault(u.Id)
        }).ToList();

        return View(new AdminUsersViewModel
        {
            Rows = new PagedResult<AdminUserRow> { Items = rows, Page = page, PageSize = pageSize, TotalCount = total },
            Keyword = q, RoleFilter = role, CurrentUserId = CurrentUserId
        });
    }

    public record RoleRequest(int Id, string Role);

    [HttpPost("nguoi-dung/vai-tro")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetRole([FromBody] RoleRequest req)
    {
        if (req.Id == CurrentUserId) return Json(new { ok = false, error = "Không thể tự đổi vai trò của chính mình." });
        var allowed = new[] { Roles.Admin, Roles.Moderator, Roles.Member };
        if (!allowed.Contains(req.Role)) return Json(new { ok = false, error = "Vai trò không hợp lệ." });

        var user = await _users.FindByIdAsync(req.Id.ToString());
        if (user is null) return Json(new { ok = false, error = "Không tìm thấy người dùng." });

        // Chốt chặn: không hạ vai trò admin cuối cùng (tránh còn 0 admin).
        if (req.Role != Roles.Admin && await _users.IsInRoleAsync(user, Roles.Admin))
        {
            var admins = await _users.GetUsersInRoleAsync(Roles.Admin);
            if (admins.Count <= 1) return Json(new { ok = false, error = "Không thể hạ vai trò admin cuối cùng." });
        }

        var current = await _users.GetRolesAsync(user);
        await _users.RemoveFromRolesAsync(user, current.Intersect(allowed));
        await _users.AddToRoleAsync(user, req.Role);
        // Đổi vai trò → làm mới security stamp để claim vai trò mới có hiệu lực ở phiên đang mở (≤2 phút).
        await _users.UpdateSecurityStampAsync(user);
        return Json(new { ok = true, role = req.Role });
    }

    public record IdRequest(int Id);

    [HttpPost("nguoi-dung/khoa")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLock([FromBody] IdRequest req)
    {
        if (req.Id == CurrentUserId) return Json(new { ok = false, error = "Không thể tự khóa chính mình." });
        var user = await _users.FindByIdAsync(req.Id.ToString());
        if (user is null) return Json(new { ok = false, error = "Không tìm thấy người dùng." });
        if (await _users.IsInRoleAsync(user, Roles.Admin))
            return Json(new { ok = false, error = "Không thể khóa một quản trị viên khác." });

        var locked = user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow;
        await _users.SetLockoutEnabledAsync(user, true);
        await _users.SetLockoutEndDateAsync(user, locked ? null : DateTimeOffset.MaxValue);
        // Khi khóa (đang mở → khóa): vô hiệu security stamp để đá phiên đang mở của người đó.
        if (!locked) await _users.UpdateSecurityStampAsync(user);
        return Json(new { ok = true, locked = !locked });
    }

    [HttpGet("nguoi-dung/{id:int}")]
    public async Task<IActionResult> UserDetail(int id)
    {
        var user = await _users.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();
        var roles = await _users.GetRolesAsync(user);
        SetSeo(new SeoModel { Title = $"Người dùng: {user.DisplayName}", NoIndex = true });
        return View(new AdminUserDetailViewModel
        {
            User = user,
            Role = roles.Contains(Roles.Admin) ? Roles.Admin : roles.Contains(Roles.Moderator) ? Roles.Moderator : Roles.Member,
            IsLocked = user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow,
            LockoutEnd = user.LockoutEnd,
            TopicCount = await _db.Topics.CountAsync(t => t.AuthorId == id && !t.IsDeleted),
            CommentCount = await _db.Comments.CountAsync(c => c.AuthorId == id && !c.IsDeleted),
            RecentTopics = await _db.Topics.IgnoreQueryFilters().Where(t => t.AuthorId == id)
                .OrderByDescending(t => t.CreatedAt).Take(8).ToListAsync(),
            RecentComments = await _db.Comments.IgnoreQueryFilters().Include(c => c.Topic).Where(c => c.AuthorId == id)
                .OrderByDescending(c => c.CreatedAt).Take(8).ToListAsync(),
            Warnings = await _db.UserWarnings.Where(w => w.UserId == id).OrderByDescending(w => w.CreatedAt).ToListAsync(),
            IsSelf = id == CurrentUserId
        });
    }

    public record BanRequest(int Id, int Days, string? Reason);

    [HttpPost("nguoi-dung/cam")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BanUser([FromBody] BanRequest req)
    {
        if (req.Id == CurrentUserId) return Json(new { ok = false, error = "Không thể tự khóa chính mình." });
        var user = await _users.FindByIdAsync(req.Id.ToString());
        if (user is null) return Json(new { ok = false, error = "Không tìm thấy người dùng." });
        if (await _users.IsInRoleAsync(user, Roles.Admin)) return Json(new { ok = false, error = "Không thể khóa một quản trị viên khác." });

        var until = req.Days <= 0 ? DateTimeOffset.MaxValue : DateTimeOffset.UtcNow.AddDays(req.Days);
        await _users.SetLockoutEnabledAsync(user, true);
        await _users.SetLockoutEndDateAsync(user, until);
        var label = req.Days <= 0 ? "Khóa vĩnh viễn" : $"Khóa {req.Days} ngày";
        _db.UserWarnings.Add(new UserWarning
        {
            UserId = req.Id, ModeratorId = CurrentUserId, CreatedAt = DateTime.UtcNow,
            Reason = label + (string.IsNullOrWhiteSpace(req.Reason) ? "" : $": {req.Reason.Trim()}")
        });
        await _db.SaveChangesAsync();
        // Vô hiệu security stamp để đá phiên đang mở của người bị cấm (không gửi thông báo —
        // họ không đăng nhập được để đọc; màn đăng nhập sẽ báo "tài khoản bị khóa").
        await _users.UpdateSecurityStampAsync(user);
        return Json(new { ok = true });
    }

    public record WarnRequest(int Id, string Reason);

    [HttpPost("nguoi-dung/canh-cao")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WarnUser([FromBody] WarnRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Reason)) return Json(new { ok = false, error = "Nhập lý do cảnh cáo." });
        var user = await _users.FindByIdAsync(req.Id.ToString());
        if (user is null) return Json(new { ok = false, error = "Không tìm thấy người dùng." });
        _db.UserWarnings.Add(new UserWarning { UserId = req.Id, ModeratorId = CurrentUserId, Reason = req.Reason.Trim(), CreatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();
        await _notifications.NotifyModerationAsync(req.Id, $"Bạn nhận một cảnh cáo từ ban quản trị: {req.Reason.Trim()}", "/bang-tin");
        return Json(new { ok = true, count = await _db.UserWarnings.CountAsync(w => w.UserId == req.Id) });
    }

    // ---------------- Analytics ----------------
    [HttpGet("phan-tich")]
    public async Task<IActionResult> Analytics()
    {
        SetSeo(new SeoModel { Title = "Phân tích", NoIndex = true });
        var since = DateTime.UtcNow.AddDays(-30);
        var vm = new AdminAnalyticsViewModel
        {
            NewTopics30 = await _db.Topics.CountAsync(t => t.CreatedAt >= since),
            NewComments30 = await _db.Comments.CountAsync(c => c.CreatedAt >= since),
            NewUsers30 = await _db.Users.CountAsync(u => u.CreatedAt >= since),
            ActiveUsers30 = await _db.Users.CountAsync(u => u.LastActiveAt >= since),
            ApprovedTopics = await _db.Topics.CountAsync(t => t.IsApproved),
            PendingTopics = await _db.Topics.IgnoreQueryFilters().CountAsync(t => !t.IsApproved && !t.IsDeleted),
            TopTopics = await _db.Topics.OrderByDescending(t => t.ViewCount).Take(10)
                .Select(t => new AnalyticsTopic(t.Title, $"/chu-de/{t.Id}/{t.Slug}", t.ViewCount, t.Score)).ToListAsync(),
            TopUsers = await _db.Users.OrderByDescending(u => u.Reputation).Take(10)
                .Select(u => new AnalyticsUser(u.DisplayName, u.UserName!, u.Reputation)).ToListAsync()
        };
        // Phân bố chủ đề theo giờ (UTC+7), tính trong bộ nhớ để trung lập provider.
        var times = await _db.Topics.Where(t => t.CreatedAt >= since).Select(t => t.CreatedAt).ToListAsync();
        foreach (var t in times) vm.TopicsByHour[(t.Hour + 7) % 24]++;
        return View(vm);
    }

    // ---------------- Categories ----------------
    [HttpGet("danh-muc")]
    public async Task<IActionResult> Categories()
    {
        SetSeo(new SeoModel { Title = "Quản lý danh mục", NoIndex = true });
        var cats = await _db.Categories.OrderBy(c => c.DisplayOrder).ToListAsync();
        var counts = await _db.Topics.Where(t => !t.IsDeleted).GroupBy(t => t.CategoryId)
            .Select(g => new { g.Key, C = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.C);
        var mods = await (from cm in _db.CategoryModerators
                          join u in _db.Users on cm.UserId equals u.Id
                          select new { cm.CategoryId, u.UserName }).ToListAsync();
        var modMap = mods.GroupBy(x => x.CategoryId).ToDictionary(g => g.Key, g => g.Select(x => x.UserName!).ToList());
        return View(new AdminCategoriesViewModel { Categories = cats, TopicCounts = counts, Moderators = modMap });
    }

    [HttpPost("danh-muc/luu")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCategory(CategoryEditModel m)
    {
        if (string.IsNullOrWhiteSpace(m.Name)) { Toast("Tên danh mục không được trống.", "error"); return RedirectToAction(nameof(Categories)); }
        var slug = _slug.Generate(m.Name, 120);
        var dupSlug = await _db.Categories.AnyAsync(c => c.Slug == slug && c.Id != m.Id);
        if (dupSlug) { Toast($"Slug \"{slug}\" đã tồn tại, đổi tên khác.", "error"); return RedirectToAction(nameof(Categories)); }

        Category cat;
        if (m.Id > 0)
        {
            cat = await _db.Categories.FindAsync(m.Id) ?? throw new InvalidOperationException();
            cat.Name = m.Name.Trim(); cat.Slug = slug; cat.Description = m.Description?.Trim();
            cat.IconName = string.IsNullOrWhiteSpace(m.IconName) ? "door-open" : m.IconName.Trim();
            cat.ColorHex = string.IsNullOrWhiteSpace(m.ColorHex) ? "#4f8cff" : m.ColorHex.Trim();
            cat.DisplayOrder = m.DisplayOrder;
            cat.RequireApproval = m.RequireApproval;
            Toast("Đã cập nhật danh mục.");
        }
        else
        {
            var maxOrder = await _db.Categories.Select(c => (int?)c.DisplayOrder).MaxAsync() ?? 0;
            cat = new Category
            {
                Name = m.Name.Trim(), Slug = slug, Description = m.Description?.Trim(),
                IconName = string.IsNullOrWhiteSpace(m.IconName) ? "door-open" : m.IconName.Trim(),
                ColorHex = string.IsNullOrWhiteSpace(m.ColorHex) ? "#4f8cff" : m.ColorHex.Trim(),
                DisplayOrder = m.DisplayOrder > 0 ? m.DisplayOrder : maxOrder + 1,
                RequireApproval = m.RequireApproval,
                CreatedAt = DateTime.UtcNow
            };
            _db.Categories.Add(cat);
            Toast("Đã thêm danh mục.");
        }
        await _db.SaveChangesAsync();

        // Đồng bộ kiểm duyệt viên phụ trách danh mục.
        var old = await _db.CategoryModerators.Where(cm => cm.CategoryId == cat.Id).ToListAsync();
        _db.CategoryModerators.RemoveRange(old);
        if (!string.IsNullOrWhiteSpace(m.Moderators))
        {
            var names = m.Moderators.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var matched = await _db.Users.Where(u => names.Contains(u.UserName!)).Select(u => u.Id).ToListAsync();
            foreach (var uid in matched)
                _db.CategoryModerators.Add(new CategoryModerator { CategoryId = cat.Id, UserId = uid });
        }
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Categories));
    }

    [HttpPost("danh-muc/xoa")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory([FromForm] int id)
    {
        var cat = await _db.Categories.FindAsync(id);
        if (cat is null) return NotFound();
        // Đếm CẢ chủ đề đã xóa mềm: chúng vẫn giữ FK (DeleteBehavior.Restrict) tới danh mục,
        // nên nếu bỏ qua thì SaveChanges sẽ ném lỗi ràng buộc khóa ngoại (500) thay vì báo rõ.
        var topicCount = await _db.Topics.IgnoreQueryFilters().CountAsync(t => t.CategoryId == id);
        if (topicCount > 0)
        {
            Toast($"Không xóa được: danh mục còn {topicCount} chủ đề (gồm cả đã xóa mềm). Hãy chuyển hoặc xóa hẳn chúng trước.", "error");
            return RedirectToAction(nameof(Categories));
        }
        _db.Categories.Remove(cat);
        await _db.SaveChangesAsync();
        Toast("Đã xóa danh mục.");
        return RedirectToAction(nameof(Categories));
    }

    [HttpPost("danh-muc/thu-tu")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReorderCategories([FromBody] int[] ids)
    {
        var cats = await _db.Categories.ToListAsync();
        for (var i = 0; i < ids.Length; i++)
        {
            var cat = cats.FirstOrDefault(c => c.Id == ids[i]);
            if (cat != null) cat.DisplayOrder = i + 1;
        }
        await _db.SaveChangesAsync();
        return Json(new { ok = true });
    }

    // ---------------- Tags ----------------
    [HttpGet("the")]
    public async Task<IActionResult> Tags([FromQuery(Name = "q")] string? q, [FromQuery(Name = "trang")] int trang = 1)
    {
        SetSeo(new SeoModel { Title = "Quản lý thẻ", NoIndex = true });
        const int pageSize = 30;
        var page = Math.Max(1, trang);
        IQueryable<Tag> query = _db.Tags;
        if (!string.IsNullOrWhiteSpace(q))
        {
            var kw = $"%{q.Trim()}%";
            query = query.Where(t => EF.Functions.Like(t.Name, kw) || EF.Functions.Like(t.Slug, kw));
        }
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(t => t.UseCount).ThenBy(t => t.Name)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return View(new AdminTagsViewModel
        {
            Tags = new PagedResult<Tag> { Items = items, Page = page, PageSize = pageSize, TotalCount = total },
            Keyword = q
        });
    }

    public record TagRenameRequest(int Id, string Name);

    [HttpPost("the/sua")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RenameTag([FromBody] TagRenameRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) return Json(new { ok = false, error = "Tên thẻ không được trống." });
        var tag = await _db.Tags.FindAsync(req.Id);
        if (tag is null) return Json(new { ok = false, error = "Không tìm thấy thẻ." });
        var slug = _slug.Generate(req.Name, 50);
        if (await _db.Tags.AnyAsync(t => t.Slug == slug && t.Id != req.Id))
            return Json(new { ok = false, error = "Đã có thẻ khác dùng slug này." });
        tag.Name = req.Name.Trim();
        tag.Slug = slug;
        await _db.SaveChangesAsync();
        return Json(new { ok = true, name = tag.Name, slug = tag.Slug });
    }

    public record TagMergeRequest(int FromId, string IntoName);

    [HttpPost("the/gop")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MergeTag([FromBody] TagMergeRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.IntoName)) return Json(new { ok = false, error = "Nhập tên thẻ đích." });
        var from = await _db.Tags.FindAsync(req.FromId);
        if (from is null) return Json(new { ok = false, error = "Không tìm thấy thẻ nguồn." });
        var intoSlug = _slug.Generate(req.IntoName, 50);
        var into = await _db.Tags.FirstOrDefaultAsync(t => t.Slug == intoSlug);
        if (into is null) return Json(new { ok = false, error = $"Không có thẻ \"{req.IntoName}\" để gộp vào." });
        if (into.Id == from.Id) return Json(new { ok = false, error = "Không thể gộp thẻ vào chính nó." });

        var fromTags = await _db.TopicTags.Where(tt => tt.TagId == from.Id).ToListAsync();
        var intoTopicIds = (await _db.TopicTags.Where(tt => tt.TagId == into.Id).Select(tt => tt.TopicId).ToListAsync()).ToHashSet();
        foreach (var tt in fromTags)
        {
            if (intoTopicIds.Add(tt.TopicId))
                _db.TopicTags.Add(new TopicTag { TopicId = tt.TopicId, TagId = into.Id });
        }
        _db.TopicTags.RemoveRange(fromTags);
        _db.Tags.Remove(from);
        into.UseCount = intoTopicIds.Count;
        await _db.SaveChangesAsync();
        return Json(new { ok = true, into = into.Name, useCount = into.UseCount });
    }

    [HttpPost("the/xoa")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTag([FromBody] IdRequest req)
    {
        var tag = await _db.Tags.FindAsync(req.Id);
        if (tag is null) return Json(new { ok = false, error = "Không tìm thấy thẻ." });
        var links = await _db.TopicTags.Where(tt => tt.TagId == req.Id).ToListAsync();
        _db.TopicTags.RemoveRange(links);
        _db.Tags.Remove(tag);
        await _db.SaveChangesAsync();
        return Json(new { ok = true });
    }

    // ---------------- Settings ----------------
    [HttpGet("cau-hinh")]
    public IActionResult Settings()
    {
        SetSeo(new SeoModel { Title = "Cấu hình site", NoIndex = true });
        return View(new AdminSettingsViewModel
        {
            SiteName = _settings.SiteName,
            SiteDescription = _settings.SiteDescription,
            BannedWords = string.Join(", ", _settings.BannedWords),
            FeatureRegistration = _settings.GetBool(SettingKeys.FeatureRegistration, true),
            FeaturePosting = _settings.GetBool(SettingKeys.FeaturePosting, true),
            FeatureChat = _settings.GetBool(SettingKeys.FeatureChat, true),
            FeaturePolls = _settings.GetBool(SettingKeys.FeaturePolls, true),
            BrandAccent = _settings.Get(SettingKeys.BrandAccent, ""),
            BrandLogo = _settings.Get(SettingKeys.BrandLogo, ""),
            BrandFavicon = _settings.Get(SettingKeys.BrandFavicon, ""),
            AutomodNewUser = _settings.GetBool(SettingKeys.AutomodNewUser, false),
            AutomodNewUserDays = _settings.GetInt(SettingKeys.AutomodNewUserDays, 3),
            AutomodSpam = _settings.GetBool(SettingKeys.AutomodSpam, true),
            RateTopicsPerHour = _settings.GetInt(SettingKeys.RateTopicsPerHour, 0),
            RateCommentsPerMinute = _settings.GetInt(SettingKeys.RateCommentsPerMinute, 0),
            CannedReasons = _settings.Get(SettingKeys.CannedReasons, "")
        });
    }

    [HttpPost("cau-hinh")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Settings(AdminSettingsViewModel m)
    {
        await _settings.SaveAsync(new Dictionary<string, string>
        {
            [SettingKeys.SiteName] = m.SiteName?.Trim() ?? "",
            [SettingKeys.SiteDescription] = m.SiteDescription?.Trim() ?? "",
            [SettingKeys.BannedWords] = m.BannedWords?.Trim() ?? "",
            [SettingKeys.FeatureRegistration] = m.FeatureRegistration ? "true" : "false",
            [SettingKeys.FeaturePosting] = m.FeaturePosting ? "true" : "false",
            [SettingKeys.FeatureChat] = m.FeatureChat ? "true" : "false",
            [SettingKeys.FeaturePolls] = m.FeaturePolls ? "true" : "false",
            [SettingKeys.BrandAccent] = m.BrandAccent?.Trim() ?? "",
            [SettingKeys.BrandLogo] = m.BrandLogo?.Trim() ?? "",
            [SettingKeys.BrandFavicon] = m.BrandFavicon?.Trim() ?? "",
            [SettingKeys.AutomodNewUser] = m.AutomodNewUser ? "true" : "false",
            [SettingKeys.AutomodNewUserDays] = m.AutomodNewUserDays.ToString(),
            [SettingKeys.AutomodSpam] = m.AutomodSpam ? "true" : "false",
            [SettingKeys.RateTopicsPerHour] = Math.Max(0, m.RateTopicsPerHour).ToString(),
            [SettingKeys.RateCommentsPerMinute] = Math.Max(0, m.RateCommentsPerMinute).ToString(),
            [SettingKeys.CannedReasons] = m.CannedReasons?.Trim() ?? ""
        });
        Toast("Đã lưu cấu hình.");
        return RedirectToAction(nameof(Settings));
    }

    // Tải ảnh dùng cho thương hiệu (logo/favicon). Chỉ nhận ảnh raster.
    [HttpPost("tai-anh")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> UploadImage(IFormFile? file)
    {
        if (file is null || file.Length == 0) return BadRequest(new { message = "Chưa chọn tệp." });
        if (file.Length > 5 * 1024 * 1024) return BadRequest(new { message = "Tệp vượt quá 5MB." });
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".ico" }.Contains(ext))
            return BadRequest(new { message = "Định dạng ảnh không hỗ trợ." });
        var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var dir = Path.Combine(webRoot, "uploads");
        Directory.CreateDirectory(dir);
        var stored = $"brand-{Guid.NewGuid():N}{ext}";
        await using (var fs = System.IO.File.Create(Path.Combine(dir, stored)))
            await file.CopyToAsync(fs);
        return Json(new { url = $"/uploads/{stored}" });
    }

    // ---------------- Trang tĩnh (CMS) ----------------
    [HttpGet("trang")]
    public async Task<IActionResult> Pages()
    {
        SetSeo(new SeoModel { Title = "Trang tĩnh", NoIndex = true });
        return View(new AdminPagesViewModel { Pages = await _db.CmsPages.OrderBy(p => p.Slug).ToListAsync() });
    }

    [HttpGet("trang/sua")]
    public async Task<IActionResult> EditPage(int id = 0)
    {
        SetSeo(new SeoModel { Title = "Sửa trang", NoIndex = true });
        var m = new CmsPageEditModel();
        if (id > 0)
        {
            var p = await _db.CmsPages.FindAsync(id);
            if (p is null) return NotFound();
            m = new CmsPageEditModel { Id = p.Id, Title = p.Title, Slug = p.Slug, Body = p.Body, IsPublished = p.IsPublished };
        }
        return View(m);
    }

    [HttpPost("trang/luu")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePage(CmsPageEditModel m)
    {
        if (string.IsNullOrWhiteSpace(m.Title)) { Toast("Tiêu đề không được trống.", "error"); return RedirectToAction(nameof(Pages)); }
        var slug = _slug.Generate(string.IsNullOrWhiteSpace(m.Slug) ? m.Title : m.Slug, 80);

        var existing = m.Id > 0 ? await _db.CmsPages.FindAsync(m.Id) : null;
        if (m.Id > 0 && existing is null) return NotFound();
        // Trang Nội quy có URL cố định /noi-quy (footer + fallback trỏ tới) — không cho đổi slug đi.
        if (existing?.Slug == "noi-quy") slug = "noi-quy";

        if (await _db.CmsPages.AnyAsync(p => p.Slug == slug && p.Id != m.Id))
        { Toast($"Slug \"{slug}\" đã tồn tại.", "error"); return RedirectToAction(nameof(Pages)); }

        if (existing is not null)
        {
            existing.Title = m.Title.Trim(); existing.Slug = slug; existing.Body = m.Body ?? ""; existing.IsPublished = m.IsPublished; existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.CmsPages.Add(new CmsPage { Title = m.Title.Trim(), Slug = slug, Body = m.Body ?? "", IsPublished = m.IsPublished, UpdatedAt = DateTime.UtcNow });
        }
        await _db.SaveChangesAsync();
        Toast("Đã lưu trang.");
        return RedirectToAction(nameof(Pages));
    }

    [HttpPost("trang/xoa")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePage([FromForm] int id)
    {
        var p = await _db.CmsPages.FindAsync(id);
        if (p != null) { _db.CmsPages.Remove(p); await _db.SaveChangesAsync(); Toast("Đã xóa trang."); }
        return RedirectToAction(nameof(Pages));
    }

    // ---------------- Thông báo chạy ----------------
    [HttpGet("thong-bao")]
    public async Task<IActionResult> Announcements()
    {
        SetSeo(new SeoModel { Title = "Thông báo chạy", NoIndex = true });
        var items = await _db.Announcements.OrderBy(a => a.DisplayOrder).ThenByDescending(a => a.CreatedAt).ToListAsync();
        return View(new AdminAnnouncementsViewModel { Items = items });
    }

    [HttpPost("thong-bao/luu")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAnnouncement(AnnouncementEditModel m)
    {
        if (string.IsNullOrWhiteSpace(m.Message)) { Toast("Nội dung thông báo không được trống.", "error"); return RedirectToAction(nameof(Announcements)); }
        var url = string.IsNullOrWhiteSpace(m.Url) ? null : m.Url.Trim();
        if (m.Id > 0)
        {
            var a = await _db.Announcements.FindAsync(m.Id);
            if (a is null) return NotFound();
            a.Message = m.Message.Trim(); a.Url = url; a.IsActive = m.IsActive;
            a.DisplayOrder = m.DisplayOrder; a.StartsAt = m.StartsAt; a.EndsAt = m.EndsAt;
            Toast("Đã cập nhật thông báo.");
        }
        else
        {
            _db.Announcements.Add(new Announcement
            {
                Message = m.Message.Trim(), Url = url, IsActive = m.IsActive,
                DisplayOrder = m.DisplayOrder, StartsAt = m.StartsAt, EndsAt = m.EndsAt, CreatedAt = DateTime.UtcNow
            });
            Toast("Đã thêm thông báo.");
        }
        await _db.SaveChangesAsync();
        _cache.Remove("ticker-items");
        return RedirectToAction(nameof(Announcements));
    }

    [HttpPost("thong-bao/xoa")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAnnouncement([FromForm] int id)
    {
        var a = await _db.Announcements.FindAsync(id);
        if (a != null) { _db.Announcements.Remove(a); await _db.SaveChangesAsync(); _cache.Remove("ticker-items"); Toast("Đã xóa thông báo."); }
        return RedirectToAction(nameof(Announcements));
    }

    // ---------------- Content: chủ đề ----------------
    [HttpGet("noi-dung")]
    public async Task<IActionResult> Content([FromQuery(Name = "q")] string? q,
        [FromQuery(Name = "danh-muc")] string? cat, [FromQuery(Name = "gom-xoa")] bool includeDeleted = false,
        [FromQuery(Name = "trang")] int trang = 1)
    {
        SetSeo(new SeoModel { Title = "Quản lý nội dung", NoIndex = true });
        const int pageSize = 20;
        var page = Math.Max(1, trang);
        IQueryable<Topic> query = _db.Topics.Include(t => t.Author).Include(t => t.Category);
        // Topic có global query filter ẩn bài đã xóa → bỏ filter khi muốn xem cả bài đã xóa.
        if (includeDeleted) query = query.IgnoreQueryFilters();
        else query = query.Where(t => !t.IsDeleted);
        if (!string.IsNullOrWhiteSpace(q)) { var kw = $"%{q.Trim()}%"; query = query.Where(t => EF.Functions.Like(t.Title, kw)); }
        if (!string.IsNullOrWhiteSpace(cat)) query = query.Where(t => t.Category.Slug == cat);
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(t => t.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return View(new AdminContentViewModel
        {
            Topics = new PagedResult<Topic> { Items = items, Page = page, PageSize = pageSize, TotalCount = total },
            Categories = await _db.Categories.OrderBy(c => c.DisplayOrder).ToListAsync(),
            Keyword = q, CategorySlug = cat, IncludeDeleted = includeDeleted
        });
    }

    public record TopicActionRequest(int Id, string Action);

    [HttpPost("noi-dung/chu-de")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TopicAction([FromBody] TopicActionRequest req)
    {
        bool ok = req.Action switch
        {
            "pin" => await _moderation.SetPinnedAsync(req.Id, CurrentUserId, true),
            "unpin" => await _moderation.SetPinnedAsync(req.Id, CurrentUserId, false),
            "lock" => await _moderation.SetLockedAsync(req.Id, CurrentUserId, true),
            "unlock" => await _moderation.SetLockedAsync(req.Id, CurrentUserId, false),
            "feature" => await _moderation.SetFeaturedAsync(req.Id, CurrentUserId, true),
            "unfeature" => await _moderation.SetFeaturedAsync(req.Id, CurrentUserId, false),
            "delete" => await _moderation.DeleteTopicAsync(req.Id, CurrentUserId, "Xóa từ quản trị"),
            "restore" => await _moderation.RestoreTopicAsync(req.Id, CurrentUserId),
            _ => false
        };
        return Json(new { ok });
    }

    [HttpPost("noi-dung/xoa-nhieu")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkDeleteTopics([FromBody] int[] ids)
    {
        var n = 0;
        foreach (var id in ids ?? Array.Empty<int>())
            if (await _moderation.DeleteTopicAsync(id, CurrentUserId, "Xóa hàng loạt từ quản trị")) n++;
        return Json(new { ok = true, deleted = n });
    }

    // ---------------- Content: bình luận ----------------
    [HttpGet("noi-dung/binh-luan")]
    public async Task<IActionResult> Comments([FromQuery(Name = "q")] string? q,
        [FromQuery(Name = "gom-xoa")] bool includeDeleted = false, [FromQuery(Name = "trang")] int trang = 1)
    {
        SetSeo(new SeoModel { Title = "Quản lý bình luận", NoIndex = true });
        const int pageSize = 25;
        var page = Math.Max(1, trang);
        IQueryable<Comment> query = _db.Comments.Include(c => c.Author).Include(c => c.Topic);
        if (includeDeleted) query = query.IgnoreQueryFilters();   // Comment có global filter ẩn bản đã xóa
        else query = query.Where(c => !c.IsDeleted);
        if (!string.IsNullOrWhiteSpace(q)) { var kw = $"%{q.Trim()}%"; query = query.Where(c => EF.Functions.Like(c.Body, kw)); }
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(c => c.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return View(new AdminCommentsViewModel
        {
            Comments = new PagedResult<Comment> { Items = items, Page = page, PageSize = pageSize, TotalCount = total },
            Keyword = q, IncludeDeleted = includeDeleted
        });
    }

    [HttpPost("noi-dung/binh-luan/xoa")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteComment([FromBody] IdRequest req)
        => Json(new { ok = await _moderation.DeleteCommentAsync(req.Id, CurrentUserId, "Xóa từ quản trị") });

    [HttpPost("noi-dung/binh-luan/khoi-phuc")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreComment([FromBody] IdRequest req)
        => Json(new { ok = await _moderation.RestoreCommentAsync(req.Id, CurrentUserId) });

    // ---------------- Huy hiệu ----------------
    [HttpGet("huy-hieu")]
    public async Task<IActionResult> Badges()
    {
        SetSeo(new SeoModel { Title = "Quản lý huy hiệu", NoIndex = true });
        var badges = await _db.Badges.OrderBy(b => b.Tier).ThenBy(b => b.Name).ToListAsync();
        var counts = await _db.UserBadges.GroupBy(b => b.BadgeId)
            .Select(g => new { g.Key, C = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.C);
        return View(new AdminBadgesViewModel
        {
            Badges = badges.Select(b => new AdminBadgeRow { Badge = b, Holders = counts.GetValueOrDefault(b.Id) }).ToList()
        });
    }

    [HttpPost("huy-hieu/luu")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveBadge(BadgeEditModel m)
    {
        if (string.IsNullOrWhiteSpace(m.Name)) { Toast("Tên huy hiệu không được trống.", "error"); return RedirectToAction(nameof(Badges)); }
        var slug = _slug.Generate(m.Name, 50);
        if (await _db.Badges.AnyAsync(b => b.Slug == slug && b.Id != m.Id)) { Toast($"Slug \"{slug}\" đã tồn tại.", "error"); return RedirectToAction(nameof(Badges)); }
        string icon = string.IsNullOrWhiteSpace(m.IconName) ? "award" : m.IconName.Trim();
        string color = string.IsNullOrWhiteSpace(m.ColorHex) ? "#c9a227" : m.ColorHex.Trim();
        if (m.Id > 0)
        {
            var b = await _db.Badges.FindAsync(m.Id);
            if (b is null) return NotFound();
            b.Name = m.Name.Trim(); b.Slug = slug; b.Description = m.Description?.Trim() ?? ""; b.IconName = icon; b.ColorHex = color; b.Tier = m.Tier;
            Toast("Đã cập nhật huy hiệu.");
        }
        else
        {
            _db.Badges.Add(new Badge { Name = m.Name.Trim(), Slug = slug, Description = m.Description?.Trim() ?? "", IconName = icon, ColorHex = color, Tier = m.Tier });
            Toast("Đã thêm huy hiệu.");
        }
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Badges));
    }

    [HttpPost("huy-hieu/xoa")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBadge([FromForm] int id)
    {
        var badge = await _db.Badges.FindAsync(id);
        if (badge is null) return NotFound();
        var awarded = await _db.UserBadges.Where(ub => ub.BadgeId == id).ToListAsync();
        _db.UserBadges.RemoveRange(awarded);
        _db.Badges.Remove(badge);
        await _db.SaveChangesAsync();
        Toast($"Đã xóa huy hiệu (gỡ khỏi {awarded.Count} người).");
        return RedirectToAction(nameof(Badges));
    }

    public record BadgeAwardRequest(int BadgeId, string UserName);

    [HttpPost("huy-hieu/trao")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AwardBadge([FromBody] BadgeAwardRequest req)
    {
        var user = await _users.FindByNameAsync(req.UserName?.Trim() ?? "");
        if (user is null) return Json(new { ok = false, error = "Không tìm thấy người dùng." });
        if (!await _db.Badges.AnyAsync(b => b.Id == req.BadgeId)) return Json(new { ok = false, error = "Huy hiệu không tồn tại." });
        if (await _db.UserBadges.AnyAsync(ub => ub.BadgeId == req.BadgeId && ub.UserId == user.Id))
            return Json(new { ok = false, error = "Người này đã có huy hiệu." });
        _db.UserBadges.Add(new UserBadge { BadgeId = req.BadgeId, UserId = user.Id, AwardedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();
        return Json(new { ok = true, holders = await _db.UserBadges.CountAsync(ub => ub.BadgeId == req.BadgeId) });
    }

    [HttpPost("huy-hieu/thu-hoi")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeBadge([FromBody] BadgeAwardRequest req)
    {
        var user = await _users.FindByNameAsync(req.UserName?.Trim() ?? "");
        if (user is null) return Json(new { ok = false, error = "Không tìm thấy người dùng." });
        var ub = await _db.UserBadges.FirstOrDefaultAsync(x => x.BadgeId == req.BadgeId && x.UserId == user.Id);
        if (ub is null) return Json(new { ok = false, error = "Người này chưa có huy hiệu." });
        _db.UserBadges.Remove(ub);
        await _db.SaveChangesAsync();
        return Json(new { ok = true, holders = await _db.UserBadges.CountAsync(x => x.BadgeId == req.BadgeId) });
    }

    // ---------------- Xuất CSV ----------------
    [HttpGet("xuat/nguoi-dung")]
    public async Task<IActionResult> ExportUsers()
    {
        var users = await _db.Users.OrderBy(u => u.Id).ToListAsync();
        var roles = await (from ur in _db.UserRoles join r in _db.Roles on ur.RoleId equals r.Id
                           select new { ur.UserId, r.Name }).ToListAsync();
        var sb = new StringBuilder();
        sb.AppendLine("Id,UserName,DisplayName,Email,Role,Reputation,CreatedAt");
        foreach (var u in users)
        {
            var role = roles.FirstOrDefault(x => x.UserId == u.Id)?.Name ?? Roles.Member;
            sb.AppendLine(string.Join(",", Csv(u.Id.ToString()), Csv(u.UserName), Csv(u.DisplayName),
                Csv(u.Email), Csv(role), Csv(u.Reputation.ToString()), Csv(u.CreatedAt.ToString("o"))));
        }
        return CsvFile(sb, "nguoi-dung.csv");
    }

    [HttpGet("xuat/chu-de")]
    public async Task<IActionResult> ExportTopics()
    {
        var topics = await _db.Topics.Include(t => t.Author).Include(t => t.Category)
            .OrderBy(t => t.Id).ToListAsync();
        var sb = new StringBuilder();
        sb.AppendLine("Id,Title,Category,Author,Score,Comments,Views,IsDeleted,CreatedAt");
        foreach (var t in topics)
            sb.AppendLine(string.Join(",", Csv(t.Id.ToString()), Csv(t.Title), Csv(t.Category?.Name),
                Csv(t.Author?.DisplayName), Csv(t.Score.ToString()), Csv(t.CommentCount.ToString()),
                Csv(t.ViewCount.ToString()), Csv(t.IsDeleted ? "1" : "0"), Csv(t.CreatedAt.ToString("o"))));
        return CsvFile(sb, "chu-de.csv");
    }

    [HttpGet("xuat/binh-luan")]
    public async Task<IActionResult> ExportComments()
    {
        var comments = await _db.Comments.Include(c => c.Author)
            .Where(c => !c.IsDeleted).OrderBy(c => c.Id).ToListAsync();
        var sb = new StringBuilder();
        sb.AppendLine("Id,TopicId,Author,Score,CreatedAt,Body");
        foreach (var c in comments)
            sb.AppendLine(string.Join(",", Csv(c.Id.ToString()), Csv(c.TopicId.ToString()),
                Csv(c.Author?.DisplayName), Csv(c.Score.ToString()), Csv(c.CreatedAt.ToString("o")), Csv(c.Body)));
        return CsvFile(sb, "binh-luan.csv");
    }

    private static string Csv(string? s)
    {
        s ??= "";
        return s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r')
            ? "\"" + s.Replace("\"", "\"\"") + "\""
            : s;
    }

    private FileContentResult CsvFile(StringBuilder sb, string name)
    {
        // BOM UTF-8 để Excel đọc đúng tiếng Việt.
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", name);
    }
}
