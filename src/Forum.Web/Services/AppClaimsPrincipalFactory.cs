using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Forum.Web.Services;

/// <summary>
/// Thêm claim DisplayName / Avatar / Reputation vào cookie đăng nhập để header
/// hiển thị mà không cần truy vấn DB mỗi request.
/// </summary>
public class AppClaimsPrincipalFactory
    : UserClaimsPrincipalFactory<ApplicationUser, ApplicationRole>
{
    public AppClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options) { }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim("display_name", user.DisplayName ?? user.UserName ?? ""));
        if (!string.IsNullOrEmpty(user.AvatarUrl))
            identity.AddClaim(new Claim("avatar", user.AvatarUrl));
        identity.AddClaim(new Claim("reputation", user.Reputation.ToString()));
        return identity;
    }
}
