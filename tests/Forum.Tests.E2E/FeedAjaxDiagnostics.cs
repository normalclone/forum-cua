using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Forum.Tests.E2E;

/// <summary>
/// Xác minh feed AJAX: đổi tab sắp xếp + phân trang KHÔNG reload trang (jQuery AJAX).
/// [Explicit] -> không tính vào suite thường; chạy: dotnet test --filter Name=Feed_Ajax_No_Reload
/// </summary>
[TestFixture]
[Explicit]
public class FeedAjaxDiagnostics : TestBase
{
    private const string Dir = @"D:\Code\forum\shots";

    [Test]
    public async Task Feed_Ajax_No_Reload()
    {
        Directory.CreateDirectory(Dir);

        var pageErrors = new List<string>();
        Page.PageError += (_, err) => pageErrors.Add(err);

        await Page.GotoAsync("/");
        await Page.WaitForSelectorAsync(".topic-card");

        // Sentinel trên window: sẽ bị xoá nếu trang reload thật.
        await Page.EvaluateAsync("window.__noReload = 'sentinel-123'");
        var firstBefore = await Page.Locator(".topic-card .topic-title").First.InnerTextAsync();
        await Page.ScreenshotAsync(new() { Path = Path.Combine(Dir, "feed-01-before.png") });

        // --- Bấm tab "Nổi bật" ---
        await Page.ClickAsync(".feed-toolbar .seg a[href*='sap-xep=noi-bat']");
        await Page.WaitForURLAsync(u => u.Contains("sap-xep=noi-bat"), new() { Timeout = 8000 });
        await Page.WaitForTimeoutAsync(400);

        // 1) KHÔNG reload trang (sentinel còn nguyên).
        var sentinel = await Page.EvaluateAsync<string?>("window.__noReload");
        Assert.That(sentinel, Is.EqualTo("sentinel-123"), "Đổi tab phải KHÔNG reload trang (sentinel còn).");

        // 2) Tab "Nổi bật" thành active + danh sách vẫn hiển thị.
        await Expect(Page.Locator(".feed-toolbar .seg a[href*='sap-xep=noi-bat']"))
            .ToHaveClassAsync(new Regex("active"));
        await Expect(Page.Locator("#feed .topic-card").First).ToBeVisibleAsync();

        var firstAfter = await Page.Locator(".topic-card .topic-title").First.InnerTextAsync();
        TestContext.WriteLine($"first before='{firstBefore}' after='{firstAfter}'");
        await Page.ScreenshotAsync(new() { Path = Path.Combine(Dir, "feed-02-noi-bat.png") });

        // --- Phân trang cũng phải AJAX (nếu có) ---
        var next = Page.Locator("#feed .pager a[rel='next']");
        if (await next.CountAsync() > 0)
        {
            await next.First.ClickAsync();
            await Page.WaitForURLAsync(u => u.Contains("trang=2"), new() { Timeout = 8000 });
            await Page.WaitForTimeoutAsync(300);
            var sentinel2 = await Page.EvaluateAsync<string?>("window.__noReload");
            Assert.That(sentinel2, Is.EqualTo("sentinel-123"), "Phân trang phải KHÔNG reload trang.");
            await Expect(Page.Locator("#feed .pager .current")).ToContainTextAsync("2");
            await Page.ScreenshotAsync(new() { Path = Path.Combine(Dir, "feed-03-page2.png") });
        }

        // 3) Không có exception JS trong suốt quá trình.
        Assert.That(pageErrors, Is.Empty, "Không được có exception JS: " + string.Join(" | ", pageErrors));
        Assert.Pass();
    }

    [Test]
    public async Task Capture_Ticker()
    {
        Directory.CreateDirectory(Dir);
        await Page.GotoAsync("/");
        await Page.WaitForSelectorAsync(".ticker", new() { Timeout = 15000 });
        await Page.WaitForTimeoutAsync(400);
        var ticker = Page.Locator(".ticker");
        await ticker.ScreenshotAsync(new() { Path = Path.Combine(Dir, "ticker-red-light.png") });

        // Dark mode để chắc màu đỏ vẫn nổi.
        await Page.EvaluateAsync("window.Forum && window.Forum.theme.set('dark')");
        await Page.WaitForTimeoutAsync(300);
        await ticker.ScreenshotAsync(new() { Path = Path.Combine(Dir, "ticker-red-dark.png") });
        Assert.Pass();
    }

    [Test]
    public async Task Login_Popup_Works()
    {
        Directory.CreateDirectory(Dir);
        await Page.GotoAsync("/");

        // Bấm nút "Đăng nhập" ở header → mở popup (KHÔNG rời trang chủ).
        await Page.ClickAsync("[data-login]");
        await Page.WaitForSelectorAsync(".modal-auth form[data-login-form]", new() { Timeout = 8000 });
        await Page.WaitForTimeoutAsync(300);
        await Page.ScreenshotAsync(new() { Path = Path.Combine(Dir, "login-popup.png") });
        Assert.That(new Uri(Page.Url).AbsolutePath, Is.EqualTo("/"), "Mở popup không được rời trang.");

        // Sai mật khẩu → báo lỗi NGAY trong popup, vẫn ở trang chủ.
        await Page.FillAsync(".modal-auth input[name=UserNameOrEmail]", "demo");
        await Page.FillAsync(".modal-auth input[name=Password]", "sai-mat-khau");
        await Page.ClickAsync(".modal-auth form button[type=submit]");
        await Expect(Page.Locator(".modal-auth .validation-summary-errors")).ToContainTextAsync("không đúng", new() { Timeout = 8000 });
        await Page.ScreenshotAsync(new() { Path = Path.Combine(Dir, "login-popup-error.png") });

        // Đúng mật khẩu → đăng nhập thành công (header hiện nút Tạo chủ đề).
        await Page.FillAsync(".modal-auth input[name=UserNameOrEmail]", "demo");
        await Page.FillAsync(".modal-auth input[name=Password]", "Test@123");
        await Page.ClickAsync(".modal-auth form button[type=submit]");
        await Expect(Page.Locator("a[href='/tao-chu-de']").First).ToBeVisibleAsync(new() { Timeout = 10000 });
        Assert.Pass();
    }

    [Test]
    public async Task Capture_Featured()
    {
        Directory.CreateDirectory(Dir);
        await Page.GotoAsync("/");
        await Page.WaitForSelectorAsync(".featured-card", new() { Timeout = 15000 });
        await Page.WaitForTimeoutAsync(500);
        await Page.Locator(".featured-strip").ScreenshotAsync(new() { Path = Path.Combine(Dir, "featured-airmail.png") });

        // Hover: phải nhấc nhẹ, viền vẫn nét (không scale → không "xước").
        await Page.Locator(".featured-card").First.HoverAsync();
        await Page.WaitForTimeoutAsync(400);
        await Page.Locator(".featured-card").First.ScreenshotAsync(new() { Path = Path.Combine(Dir, "featured-airmail-hover.png") });
        Assert.Pass();
    }
}
