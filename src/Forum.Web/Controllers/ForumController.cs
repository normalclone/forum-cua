using Forum.Web.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace Forum.Web.Controllers;

/// <summary>Controller cơ sở: tiện ích chung (user id hiện tại, SEO, toast).</summary>
public abstract class ForumControllerBase : Controller
{
    protected int CurrentUserId => User.UserId();
    protected bool IsAuthed => User.Identity?.IsAuthenticated == true;

    protected void SetSeo(SeoModel seo) => ViewData["Seo"] = seo;

    protected void Toast(string message, string type = "success")
    {
        TempData["Toast"] = message;
        TempData["ToastType"] = type;
    }

    /// <summary>Trả JSON cho yêu cầu AJAX, hoặc redirect cho non-AJAX.</summary>
    protected bool IsAjax => Request.Headers["X-Requested-With"] == "XMLHttpRequest";
}
