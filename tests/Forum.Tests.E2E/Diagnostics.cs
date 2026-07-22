using Microsoft.Playwright;

namespace Forum.Tests.E2E;

/// <summary>
/// Chụp màn hình để xác minh trực quan (shadow khi scroll/chuyển trang, hover card, chat dock).
/// [Explicit] -> không chạy trong suite thường; chạy bằng: dotnet test --filter Name=Capture_Screens
/// </summary>
[TestFixture]
[Explicit]
public class Diagnostics : TestBase
{
    private const string Dir = @"D:\Code\forum\shots";

    [Test]
    public async Task Capture_Screens()
    {
        Directory.CreateDirectory(Dir);
        await LoginAsync("demo");

        await Page.GotoAsync("/");
        await Page.WaitForSelectorAsync(".topic-card");
        await Page.WaitForTimeoutAsync(700);
        await Page.ScreenshotAsync(new() { Path = Path.Combine(Dir, "01-home-top.png") });

        // Nút "Lưu" trong footer thẻ chủ đề (danh sách feed): trước & sau khi lưu.
        var footer = Page.Locator(".topic-card .topic-footer").First;
        if (await footer.CountAsync() > 0)
        {
            await footer.ScrollIntoViewIfNeededAsync();
            await footer.ScreenshotAsync(new() { Path = Path.Combine(Dir, "08-card-footer-default.png") });
            await footer.Locator("[data-bookmark]").ClickAsync();
            await Page.WaitForTimeoutAsync(600);
            await footer.ScreenshotAsync(new() { Path = Path.Combine(Dir, "09-card-footer-saved.png") });
        }

        // Cuộn xuống — kiểm tra shadow khi scroll.
        await Page.Mouse.MoveAsync(700, 450);
        await Page.Mouse.WheelAsync(0, 700);
        await Page.WaitForTimeoutAsync(500);
        await Page.ScreenshotAsync(new() { Path = Path.Combine(Dir, "02-home-scrolled.png") });

        // Hover lên card — shadow + chữ sáng lên.
        await Page.Mouse.WheelAsync(0, -700);
        await Page.WaitForTimeoutAsync(300);
        await Page.Locator(".topic-card").First.HoverAsync();
        await Page.WaitForTimeoutAsync(350);
        await Page.ScreenshotAsync(new() { Path = Path.Combine(Dir, "03-home-hover.png") });

        // Chuyển sang trang chi tiết — kiểm tra shadow khi chuyển trang.
        await Page.Locator(".topic-card .topic-title").First.ClickAsync();
        await Page.WaitForSelectorAsync("article h1");
        await Page.WaitForTimeoutAsync(700);
        await Page.ScreenshotAsync(new() { Path = Path.Combine(Dir, "04-topic.png") });

        // Nút Lưu: chụp thanh nút trạng thái mặc định + đã lưu để so style chung.
        var bar = Page.Locator(".flex.wrap.gap-6.mt-14").First;
        if (await bar.CountAsync() > 0)
        {
            await bar.ScrollIntoViewIfNeededAsync();
            await Page.WaitForTimeoutAsync(300);
            await bar.ScreenshotAsync(new() { Path = Path.Combine(Dir, "04a-actionbar-default.png") });
            var bm = Page.Locator("[data-bookmark]").First;
            await bm.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
            await bar.ScreenshotAsync(new() { Path = Path.Combine(Dir, "04b-save-saved.png") });
        }

        // Trang danh mục — strip "Ghim & nổi bật" riêng của danh mục ở đầu trang.
        await Page.GotoAsync("/danh-muc/cua-go");
        await Page.WaitForSelectorAsync(".featured-strip");
        await Page.WaitForTimeoutAsync(500);
        await Page.ScreenshotAsync(new() { Path = Path.Combine(Dir, "10-category-featured.png") });

        // Hover tiêu đề bài → preview nội dung chủ đề.
        await Page.GotoAsync("/");
        await Page.WaitForSelectorAsync(".topic-card .topic-title[data-topic-preview]");
        await Page.Locator(".topic-card .topic-title[data-topic-preview]").First.HoverAsync();
        await Page.WaitForSelectorAsync(".hovercard .tp-card");
        await Page.WaitForTimeoutAsync(500);
        await Page.ScreenshotAsync(new() { Path = Path.Combine(Dir, "11-topic-preview.png") });

        // Mở dock chat.
        await Page.ClickAsync("#chat-launcher");
        await Page.WaitForSelectorAsync(".chat-list-panel");
        await Page.WaitForTimeoutAsync(600);
        await Page.ScreenshotAsync(new() { Path = Path.Combine(Dir, "05-chat-list.png") });

        // Mở một cửa sổ hội thoại (demo đã có hội thoại seed).
        // Mở lần lượt 4 hội thoại -> tối đa 3 cửa sổ active, cái thứ 4 đẩy cái cũ nhất về avatar (hàng chờ).
        for (var i = 0; i < 4; i++)
        {
            if (await Page.Locator(".chat-list-panel").CountAsync() == 0)
                await Page.ClickAsync("#chat-launcher");   // mở danh sách nếu đang đóng
            await Page.WaitForSelectorAsync(".chat-list-panel .chat-list-item");
            var items = Page.Locator(".chat-list-panel .chat-list-item");
            if (await items.CountAsync() <= i) break;
            await items.Nth(i).ClickAsync();               // mở hội thoại + tự đóng danh sách
            await Page.WaitForTimeoutAsync(500);
        }
        await Page.WaitForTimeoutAsync(500);
        var winCount = await Page.Locator(".chat-window").CountAsync();
        var headCount = await Page.Locator(".chat-head").CountAsync();
        TestContext.WriteLine($"windows={winCount} heads={headCount}");
        await Page.ScreenshotAsync(new() { Path = Path.Combine(Dir, "06-chat-multi.png") });

        // Thu nhỏ một cửa sổ -> nó về avatar (hàng chờ).
        if (winCount > 0)
        {
            await Page.Locator(".chat-window [data-min]").First.ClickAsync();
            await Page.WaitForTimeoutAsync(400);
            await Page.ScreenshotAsync(new() { Path = Path.Combine(Dir, "07-chat-minimized.png") });
        }

        // Tối đa 3 cửa sổ active.
        Assert.That(winCount, Is.LessThanOrEqualTo(3), "Tối đa 3 cửa sổ active.");
        Assert.Pass();
    }
}
