using Forum.Web.Models.ViewModels;
using Forum.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Forum.Web.Controllers;

public class AccountController : ForumControllerBase
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly IReputationService _reputation;
    private readonly ISiteSettingService _settings;

    public AccountController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
        IReputationService reputation, ISiteSettingService settings)
    {
        _users = users;
        _signIn = signIn;
        _reputation = reputation;
        _settings = settings;
    }

    private bool RegistrationOpen => _settings.GetBool(SettingKeys.FeatureRegistration, true);

    [HttpGet("/dang-ky")]
    [AllowAnonymous]
    public IActionResult Register(string? returnUrl = null)
    {
        if (IsAuthed) return RedirectToAction("Index", "Home");
        if (!RegistrationOpen)
        {
            Toast("Đăng ký thành viên đang tạm khóa.", "warning");
            return RedirectToAction("Login");
        }
        SetSeo(new SeoModel { Title = "Đăng ký tài khoản", Description = "Tạo tài khoản để tham gia thảo luận về cửa.", NoIndex = true });
        return View(new RegisterViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("/dang-ky")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel vm)
    {
        if (!RegistrationOpen) { Toast("Đăng ký thành viên đang tạm khóa.", "warning"); return RedirectToAction("Login"); }
        if (!ModelState.IsValid) { SetSeo(new SeoModel { Title = "Đăng ký tài khoản", NoIndex = true }); return View(vm); }

        if (await _users.FindByNameAsync(vm.UserName) is not null)
            ModelState.AddModelError(nameof(vm.UserName), "Tên đăng nhập đã tồn tại.");
        if (await _users.FindByEmailAsync(vm.Email) is not null)
            ModelState.AddModelError(nameof(vm.Email), "Email đã được sử dụng.");
        if (!ModelState.IsValid) { SetSeo(new SeoModel { Title = "Đăng ký tài khoản", NoIndex = true }); return View(vm); }

        var user = new ApplicationUser
        {
            UserName = vm.UserName,
            Email = vm.Email,
            EmailConfirmed = true,
            DisplayName = vm.DisplayName,
            Trade = vm.Trade,
            CreatedAt = DateTime.UtcNow,
            LastActiveAt = DateTime.UtcNow
        };
        var result = await _users.CreateAsync(user, vm.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
            SetSeo(new SeoModel { Title = "Đăng ký tài khoản", NoIndex = true });
            return View(vm);
        }

        await _users.AddToRoleAsync(user, Roles.Member);
        await _reputation.CheckAndAwardBadgesAsync(user.Id);
        await _signIn.SignInAsync(user, isPersistent: true);
        Toast($"Chào mừng {user.DisplayName}! Tài khoản đã được tạo.");
        return RedirectToLocal(vm.ReturnUrl);
    }

    [HttpGet("/dang-nhap")]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (IsAuthed) return IsAjax ? Json(new { ok = true, redirect = "/" }) : RedirectToAction("Index", "Home");
        var vm = new LoginViewModel { ReturnUrl = returnUrl };
        if (IsAjax) return PartialView("_LoginForm", vm);   // chỉ form, để nạp vào popup
        SetSeo(new SeoModel { Title = "Đăng nhập", Description = "Đăng nhập vào Diễn đàn Cửa.", NoIndex = true });
        return View(vm);
    }

    [HttpPost("/dang-nhap")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel vm)
    {
        if (!ModelState.IsValid)
            return IsAjax ? PartialView("_LoginForm", vm) : LoginFullView(vm);

        // Cho phép đăng nhập bằng email hoặc username.
        var userName = vm.UserNameOrEmail.Trim();
        if (userName.Contains('@'))
        {
            var byEmail = await _users.FindByEmailAsync(userName);
            if (byEmail?.UserName is not null) userName = byEmail.UserName;
        }

        var result = await _signIn.PasswordSignInAsync(userName, vm.Password, vm.RememberMe, lockoutOnFailure: false);
        if (result.IsLockedOut)
        {
            ModelState.AddModelError("", "Tài khoản của bạn đang bị khóa. Vui lòng liên hệ ban quản trị.");
            return IsAjax ? PartialView("_LoginForm", vm) : LoginFullView(vm);
        }
        if (!result.Succeeded)
        {
            ModelState.AddModelError("", "Tên đăng nhập hoặc mật khẩu không đúng.");
            return IsAjax ? PartialView("_LoginForm", vm) : LoginFullView(vm);
        }

        var user = await _users.FindByNameAsync(userName);
        if (user is not null) { user.LastActiveAt = DateTime.UtcNow; await _users.UpdateAsync(user); }

        Toast("Đăng nhập thành công.");
        // Popup: trả JSON để JS tự điều hướng (TempData toast hiện ở trang đích).
        if (IsAjax)
            return Json(new { ok = true, redirect = Url.IsLocalUrl(vm.ReturnUrl) ? vm.ReturnUrl! : Url.Action("Index", "Home")! });
        return RedirectToLocal(vm.ReturnUrl);
    }

    private ViewResult LoginFullView(LoginViewModel vm)
    {
        SetSeo(new SeoModel { Title = "Đăng nhập", NoIndex = true });
        return View("Login", vm);
    }

    [HttpPost("/dang-xuat")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        Toast("Đã đăng xuất.");
        return RedirectToAction("Index", "Home");
    }

    [HttpGet("/tu-choi-truy-cap")]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        SetSeo(new SeoModel { Title = "Không có quyền truy cập", NoIndex = true });
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    private IActionResult RedirectToLocal(string? returnUrl)
        => Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction("Index", "Home");
}
