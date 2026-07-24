using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Forum.Tests.E2E;

public class TestBase : PageTest
{
    protected const string Password = "Test@123";

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        BaseURL = ServerFixture.BaseUrl,
        IgnoreHTTPSErrors = true,
        ViewportSize = new ViewportSize { Width = 1280, Height = 920 },
        Locale = "vi-VN",
        ColorScheme = ColorScheme.Light // mặc định sáng để test dark mode tất định
    };

    protected async Task LoginAsync(string userName, string password = Password)
    {
        await Page.GotoAsync("/dang-nhap");
        await Page.FillAsync("input[name=UserNameOrEmail]", userName);
        await Page.FillAsync("input[name=Password]", password);
        await Page.ClickAsync("form button[type=submit]");
        await Page.WaitForURLAsync(u => !u.Contains("/dang-nhap"), new() { Timeout = 15000 });
    }

    protected async Task LogoutAsync()
    {
        await Page.GotoAsync("/");
        await Page.ClickAsync("[data-menu=user-menu]");
        await Page.ClickAsync("#user-menu form[action='/dang-xuat'] button[type=submit]");
        await Page.WaitForURLAsync("**/", new() { Timeout = 15000 });
    }

    /// <summary>Mở chủ đề đầu tiên trong feed trang chủ, trả về URL chi tiết.</summary>
    protected async Task<string> OpenFirstTopicAsync()
    {
        await Page.GotoAsync("/");
        await Page.Locator(".topic-card .topic-title").First.ClickAsync();
        await Page.WaitForSelectorAsync("article h1");
        return Page.Url;
    }

    /// <summary>Mở một chủ đề (không khóa) trong danh mục cho trước.</summary>
    protected async Task<string> OpenCategoryTopicAsync(string categorySlug = "ket-cau-thi-cong")
    {
        await Page.GotoAsync($"/danh-muc/{categorySlug}");
        await Page.Locator(".topic-card .topic-title").First.ClickAsync();
        await Page.WaitForSelectorAsync("article h1");
        return Page.Url;
    }

    protected static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8];
}
