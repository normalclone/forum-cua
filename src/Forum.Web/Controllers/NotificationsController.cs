using Forum.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Forum.Web.Controllers;

[Authorize]
public class NotificationsController : ForumControllerBase
{
    private readonly INotificationService _notifications;
    public NotificationsController(INotificationService notifications) => _notifications = notifications;

    [HttpGet("/thong-bao")]
    public async Task<IActionResult> Index()
    {
        SetSeo(new SeoModel { Title = "Thông báo", NoIndex = true });
        var list = await _notifications.RecentAsync(CurrentUserId, 50);
        return View(list);
    }

    [HttpGet("/thong-bao/dropdown")]
    public async Task<IActionResult> Dropdown()
    {
        var list = await _notifications.RecentAsync(CurrentUserId, 15);
        return PartialView("_NotificationList", list);
    }

    [HttpGet("/thong-bao/dem")]
    public async Task<IActionResult> Count()
        => Json(new { count = await _notifications.UnreadCountAsync(CurrentUserId) });

    [HttpPost("/thong-bao/{id:int}/doc")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id)
    {
        await _notifications.MarkReadAsync(CurrentUserId, id);
        return Json(new { ok = true, count = await _notifications.UnreadCountAsync(CurrentUserId) });
    }

    [HttpPost("/thong-bao/doc-tat-ca")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        await _notifications.MarkAllReadAsync(CurrentUserId);
        return Json(new { ok = true });
    }
}
