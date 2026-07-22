using Forum.Web.Helpers;
using Forum.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Forum.Web.ViewComponents;

public class NotificationBellViewComponent : ViewComponent
{
    private readonly INotificationService _notifications;

    public NotificationBellViewComponent(INotificationService notifications) => _notifications = notifications;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userId = (User as System.Security.Claims.ClaimsPrincipal)?.UserId() ?? 0;
        var count = userId > 0 ? await _notifications.UnreadCountAsync(userId) : 0;
        return View(count);
    }
}
