using System.ComponentModel.DataAnnotations;

namespace Forum.Web.Models.ViewModels;

public class RegisterViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên hiển thị")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "Tên hiển thị 2–50 ký tự")]
    [Display(Name = "Tên hiển thị")]
    public string DisplayName { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập")]
    [RegularExpression("^[a-zA-Z0-9_.]{3,30}$", ErrorMessage = "Tên đăng nhập 3–30 ký tự, chỉ chữ/số/._")]
    [Display(Name = "Tên đăng nhập")]
    public string UserName { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập email")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [Display(Name = "Email")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = "";

    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Mật khẩu nhập lại không khớp")]
    [Display(Name = "Nhập lại mật khẩu")]
    public string ConfirmPassword { get; set; } = "";

    [Display(Name = "Vai trò trong ngành cửa")]
    public UserTrade Trade { get; set; } = UserTrade.ChuNha;

    public string? ReturnUrl { get; set; }
}

public class LoginViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập hoặc email")]
    [Display(Name = "Tên đăng nhập hoặc Email")]
    public string UserNameOrEmail { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = "";

    [Display(Name = "Ghi nhớ đăng nhập")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public class EditProfileViewModel
{
    [StringLength(50, MinimumLength = 2)]
    [Display(Name = "Tên hiển thị")]
    public string DisplayName { get; set; } = "";

    [StringLength(1000, ErrorMessage = "Tiểu sử tối đa 1000 ký tự")]
    [Display(Name = "Tiểu sử")]
    public string? Bio { get; set; }

    [StringLength(120)]
    [Display(Name = "Địa điểm")]
    public string? Location { get; set; }

    [Display(Name = "Vai trò trong ngành cửa")]
    public UserTrade Trade { get; set; }

    [Url(ErrorMessage = "URL ảnh đại diện không hợp lệ")]
    [Display(Name = "URL ảnh đại diện (tuỳ chọn)")]
    public string? AvatarUrl { get; set; }
}
