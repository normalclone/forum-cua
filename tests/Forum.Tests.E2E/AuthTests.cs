using Microsoft.Playwright;

namespace Forum.Tests.E2E;

[TestFixture]
public class AuthTests : TestBase
{
    [Test]
    public async Task Register_Then_Login_Then_Logout()
    {
        var uname = Unique("kt");
        await Page.GotoAsync("/dang-ky");
        await Page.FillAsync("input[name=DisplayName]", "Người Kiểm Thử");
        await Page.FillAsync("input[name=UserName]", uname);
        await Page.FillAsync("input[name=Email]", uname + "@test.vn");
        await Page.FillAsync("input[name=Password]", Password);
        await Page.FillAsync("input[name=ConfirmPassword]", Password);
        await Page.ClickAsync("form button[type=submit]");

        await Page.WaitForURLAsync(u => !u.Contains("/dang-ky"), new() { Timeout = 15000 });
        // Đã đăng nhập: nút "Tạo chủ đề" hiển thị trên header.
        await Expect(Page.Locator("a[href='/tao-chu-de']").First).ToBeVisibleAsync();

        await LogoutAsync();
        await Expect(Page.Locator("a[href='/dang-nhap']").First).ToBeVisibleAsync();
    }

    [Test]
    public async Task Login_With_Seeded_Demo_Account()
    {
        await LoginAsync("demo");
        await Expect(Page.Locator("a[href='/tao-chu-de']").First).ToBeVisibleAsync();
    }

    [Test]
    public async Task Login_With_Wrong_Password_Shows_Error()
    {
        await Page.GotoAsync("/dang-nhap");
        await Page.FillAsync("input[name=UserNameOrEmail]", "demo");
        await Page.FillAsync("input[name=Password]", "sai-mat-khau");
        await Page.ClickAsync("form button[type=submit]");
        await Expect(Page.Locator(".validation-summary-errors")).ToContainTextAsync("không đúng");
    }
}
