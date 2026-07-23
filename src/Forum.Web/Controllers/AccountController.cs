using System.Text;
using Forum.Web.Models.ViewModels;
using Forum.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace Forum.Web.Controllers;

public class AccountController : ForumControllerBase
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly IReputationService _reputation;
    private readonly ISiteSettingService _settings;
    private readonly IAppEmailSender _email;

    public AccountController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
        IReputationService reputation, ISiteSettingService settings, IAppEmailSender email)
    {
        _users = users;
        _signIn = signIn;
        _reputation = reputation;
        _settings = settings;
        _email = email;
    }

    private static string Enc(string t) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(t));
    private static string Dec(string t) => Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(t));

    private async Task<string> SendConfirmEmailAsync(ApplicationUser user)
    {
        var token = await _users.GenerateEmailConfirmationTokenAsync(user);
        var link = Url.Action(nameof(ConfirmEmail), "Account", new { userId = user.Id, token = Enc(token) }, Request.Scheme)!;
        await _email.SendAsync(user.Email!, "Xác nhận email — Diễn đàn Cửa",
            $"<p>Chào {user.DisplayName}, nhấn vào liên kết để xác nhận email:</p><p><a href=\"{link}\">{link}</a></p>");
        return link;
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
            EmailConfirmed = false,   // sẽ xác nhận qua email (không chặn đăng nhập)
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
        await SendConfirmEmailAsync(user);
        Toast($"Chào mừng {user.DisplayName}! Đã gửi email xác nhận (xem logs/emails ở chế độ dev).");
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

    // ---------------- Đổi mật khẩu ----------------
    [HttpGet("/doi-mat-khau")]
    [Authorize]
    public IActionResult ChangePassword()
    {
        SetSeo(new SeoModel { Title = "Đổi mật khẩu", NoIndex = true });
        return View(new ChangePasswordViewModel());
    }

    [HttpPost("/doi-mat-khau")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel vm)
    {
        if (!ModelState.IsValid) { SetSeo(new SeoModel { Title = "Đổi mật khẩu", NoIndex = true }); return View(vm); }
        var user = await _users.GetUserAsync(User);
        if (user is null) return Challenge();
        var r = await _users.ChangePasswordAsync(user, vm.CurrentPassword, vm.NewPassword);
        if (!r.Succeeded)
        {
            foreach (var e in r.Errors)
                ModelState.AddModelError("", e.Code == "PasswordMismatch" ? "Mật khẩu hiện tại không đúng." : e.Description);
            SetSeo(new SeoModel { Title = "Đổi mật khẩu", NoIndex = true });
            return View(vm);
        }
        await _signIn.RefreshSignInAsync(user);
        Toast("Đã đổi mật khẩu thành công.");
        return RedirectToAction("Settings", "Profile");
    }

    // ---------------- Quên mật khẩu ----------------
    [HttpGet("/quen-mat-khau")]
    [AllowAnonymous]
    public IActionResult ForgotPassword()
    {
        if (IsAuthed) return RedirectToAction("Index", "Home");
        SetSeo(new SeoModel { Title = "Quên mật khẩu", NoIndex = true });
        return View(new ForgotPasswordViewModel());
    }

    [HttpPost("/quen-mat-khau")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel vm)
    {
        if (!ModelState.IsValid) { SetSeo(new SeoModel { Title = "Quên mật khẩu", NoIndex = true }); return View(vm); }
        var user = await _users.FindByEmailAsync(vm.Email.Trim());
        if (user is not null)
        {
            var token = await _users.GeneratePasswordResetTokenAsync(user);
            var link = Url.Action(nameof(ResetPassword), "Account", new { email = user.Email, token = Enc(token) }, Request.Scheme)!;
            await _email.SendAsync(user.Email!, "Đặt lại mật khẩu — Diễn đàn Cửa",
                $"<p>Nhấn vào liên kết để đặt lại mật khẩu:</p><p><a href=\"{link}\">{link}</a></p><p>Nếu bạn không yêu cầu, hãy bỏ qua email này.</p>");
            if (_email.IsDevSink) ViewBag.DevLink = link;   // tiện test khi chưa có SMTP
        }
        ViewBag.Sent = true;   // luôn báo chung để tránh dò email tồn tại
        SetSeo(new SeoModel { Title = "Quên mật khẩu", NoIndex = true });
        return View(vm);
    }

    // ---------------- Đặt lại mật khẩu ----------------
    [HttpGet("/dat-lai-mat-khau")]
    [AllowAnonymous]
    public IActionResult ResetPassword(string? email, string? token)
    {
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
        { Toast("Liên kết đặt lại không hợp lệ.", "error"); return RedirectToAction("Login"); }
        SetSeo(new SeoModel { Title = "Đặt lại mật khẩu", NoIndex = true });
        return View(new ResetPasswordViewModel { Email = email, Token = token });
    }

    [HttpPost("/dat-lai-mat-khau")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel vm)
    {
        if (!ModelState.IsValid) { SetSeo(new SeoModel { Title = "Đặt lại mật khẩu", NoIndex = true }); return View(vm); }
        var user = await _users.FindByEmailAsync(vm.Email);
        if (user is not null)
        {
            IdentityResult r;
            try { r = await _users.ResetPasswordAsync(user, Dec(vm.Token), vm.NewPassword); }
            catch (FormatException) { r = IdentityResult.Failed(new IdentityError { Description = "Liên kết không hợp lệ." }); }
            if (r.Succeeded) { Toast("Đặt lại mật khẩu thành công. Hãy đăng nhập."); return RedirectToAction("Login"); }
            foreach (var e in r.Errors) ModelState.AddModelError("", e.Description);
        }
        else ModelState.AddModelError("", "Liên kết không hợp lệ hoặc đã hết hạn.");
        SetSeo(new SeoModel { Title = "Đặt lại mật khẩu", NoIndex = true });
        return View(vm);
    }

    // ---------------- Xác nhận email ----------------
    [HttpGet("/xac-nhan-email")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail(string? userId, string? token)
    {
        if (int.TryParse(userId, out var id) && !string.IsNullOrEmpty(token)
            && await _users.FindByIdAsync(id.ToString()) is { } user)
        {
            try
            {
                var r = await _users.ConfirmEmailAsync(user, Dec(token));
                Toast(r.Succeeded ? "Đã xác nhận email!" : "Liên kết xác nhận không hợp lệ hoặc đã hết hạn.",
                    r.Succeeded ? "success" : "error");
            }
            catch (FormatException) { Toast("Liên kết xác nhận không hợp lệ.", "error"); }
        }
        else Toast("Liên kết xác nhận không hợp lệ.", "error");
        return RedirectToAction("Index", "Home");
    }

    [HttpPost("/gui-lai-xac-nhan")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendConfirmation()
    {
        var user = await _users.GetUserAsync(User);
        if (user is not null && !user.EmailConfirmed) await SendConfirmEmailAsync(user);
        Toast("Đã gửi lại email xác nhận (xem logs/emails ở chế độ dev).");
        var back = Request.Headers.Referer.ToString();
        return Redirect(string.IsNullOrEmpty(back) ? "/" : back);
    }

    private IActionResult RedirectToLocal(string? returnUrl)
        => Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction("Index", "Home");
}
