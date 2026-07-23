using System.Security.Claims;

namespace Forum.Web.Helpers;

/// <summary>Quyền XEM danh mục theo vai trò tối thiểu (danh mục riêng tư).</summary>
public static class CategoryAccess
{
    public static bool CanView(ClaimsPrincipal user, string? minRole)
    {
        if (string.IsNullOrEmpty(minRole)) return true;                 // công khai
        if (user.Identity?.IsAuthenticated != true) return false;       // cần đăng nhập
        return minRole switch
        {
            "Member" => true,
            "Moderator" => user.IsInRole("Moderator") || user.IsInRole("Admin"),
            "Admin" => user.IsInRole("Admin"),
            _ => true
        };
    }
}
