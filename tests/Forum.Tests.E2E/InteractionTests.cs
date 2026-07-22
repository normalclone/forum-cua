using Microsoft.Playwright;

namespace Forum.Tests.E2E;

[TestFixture]
public class InteractionTests : TestBase
{
    [Test]
    public async Task DarkMode_Toggle_Persists_After_Reload()
    {
        await Page.GotoAsync("/");
        Assert.That(await Page.GetAttributeAsync("html", "data-theme"), Is.EqualTo("light"));
        await Page.ClickAsync("[data-theme-toggle]");
        Assert.That(await Page.GetAttributeAsync("html", "data-theme"), Is.EqualTo("dark"));
        await Page.ReloadAsync();
        Assert.That(await Page.GetAttributeAsync("html", "data-theme"), Is.EqualTo("dark"));
    }

    [Test]
    public async Task HoverCard_Appears_On_Username_Hover()
    {
        await Page.GotoAsync("/");
        await Page.Locator(".topic-card .user-link[data-username]").First.HoverAsync();
        await Expect(Page.Locator(".hovercard")).ToBeVisibleAsync(new() { Timeout = 8000 });
    }

    [Test]
    public async Task TopicPreview_Appears_On_Title_Hover()
    {
        await Page.GotoAsync("/");
        await Page.Locator(".topic-card .topic-title[data-topic-preview]").First.HoverAsync();
        await Expect(Page.Locator(".hovercard .tp-card")).ToBeVisibleAsync(new() { Timeout = 8000 });
    }

    [Test]
    public async Task TopicPreview_Prefers_Above_Cursor()
    {
        await Page.GotoAsync("/");
        await Page.Mouse.WheelAsync(0, 300);   // cuộn để có chỗ phía trên con trỏ
        await Page.Locator(".topic-card .topic-title[data-topic-preview]").First.HoverAsync();
        await Expect(Page.Locator(".hovercard .tp-card")).ToBeVisibleAsync(new() { Timeout = 8000 });
        // Ưu tiên hiện phía trên con trỏ khi đủ chỗ.
        Assert.That(await Page.Locator(".hovercard").GetAttributeAsync("data-placement"), Is.EqualTo("top"));
    }

    [Test]
    public async Task ModMenu_Opens_Above_And_Visible_For_Admin()
    {
        await LoginAsync("admin");
        await OpenCategoryTopicAsync("cua-go");
        var btn = Page.Locator("[data-menu=\"mod-menu\"]");
        await btn.ScrollIntoViewIfNeededAsync();
        await btn.ClickAsync();
        var menu = Page.Locator("#mod-menu");
        await Expect(menu).ToBeVisibleAsync(new() { Timeout = 5000 });
        // Phải mở LÊN TRÊN nút → tránh bị thẻ chủ đề (overflow:hidden) cắt mất.
        var mb = await menu.BoundingBoxAsync();
        var bb = await btn.BoundingBoxAsync();
        Assert.That(mb!.Y + mb.Height, Is.LessThanOrEqualTo(bb!.Y + 2),
            "Menu kiểm duyệt phải mở lên trên nút Kiểm duyệt.");
    }

    [Test]
    public async Task LoginPopup_Has_Working_Close_Button()
    {
        await Page.GotoAsync("/");                       // chưa đăng nhập
        await Page.ClickAsync("[data-login]");
        await Expect(Page.Locator(".modal-auth .modal-close")).ToBeVisibleAsync(new() { Timeout = 8000 });
        await Page.Locator(".modal-auth .modal-close").ClickAsync();
        await Expect(Page.Locator(".modal-auth")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Bookmark_Toggle_On_Topic()
    {
        await LoginAsync("demo");
        await OpenCategoryTopicAsync("cua-go");
        var bm = Page.Locator("[data-bookmark]").First;
        await bm.ClickAsync();
        await Expect(bm).ToContainTextAsync("Đã lưu", new() { Timeout = 8000 });
    }

    [Test]
    public async Task Demo_Has_Seeded_Notifications()
    {
        await LoginAsync("demo");
        await Page.GotoAsync("/thong-bao");
        await Expect(Page.Locator(".notif-item").First).ToBeVisibleAsync();
    }

    [Test]
    public async Task Mention_Creates_Notification_For_Mentioned_User()
    {
        await LoginAsync("demo");
        await OpenCategoryTopicAsync("cua-nhom-kinh");
        var body = "@admin " + Unique("cảm ơn anh đã tư vấn ");
        await Page.FillAsync("[data-comment-form] textarea[name=body]", body);
        await Page.Locator("[data-comment-form] button[type=submit]").First.ClickAsync();
        await Expect(Page.Locator("#comment-tree")).ToContainTextAsync("cảm ơn anh", new() { Timeout = 10000 });

        await LogoutAsync();
        await LoginAsync("admin");
        await Page.GotoAsync("/thong-bao");
        // Một bình luận @mention trong chủ đề của admin sinh nhiều thông báo (nhắc đến + trả lời);
        // chỉ cần khẳng định CÓ thông báo "nhắc đến" (lọc đúng mục, tránh strict-mode khi có >1).
        await Expect(Page.Locator(".notif-item").Filter(new() { HasText = "nhắc đến" }).First)
            .ToBeVisibleAsync(new() { Timeout = 10000 });
    }
}
