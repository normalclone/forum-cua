using System.Security.Claims;

namespace Forum.Web.Helpers;

public static class ClaimsExtensions
{
    public static int UserId(this ClaimsPrincipal user)
    {
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(id, out var n) ? n : 0;
    }

    public static string DisplayName(this ClaimsPrincipal user)
    {
        var dn = user.FindFirstValue("display_name");
        return string.IsNullOrEmpty(dn) ? (user.Identity?.Name ?? "Bạn") : dn;
    }

    public static string? Avatar(this ClaimsPrincipal user) => user.FindFirstValue("avatar");

    public static int Reputation(this ClaimsPrincipal user)
        => int.TryParse(user.FindFirstValue("reputation"), out var n) ? n : 0;

    public static bool IsStaff(this ClaimsPrincipal user)
        => user.IsInRole(Roles.Admin) || user.IsInRole(Roles.Moderator);
}
