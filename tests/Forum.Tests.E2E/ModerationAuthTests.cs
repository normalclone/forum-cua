using Microsoft.Playwright;

namespace Forum.Tests.E2E;

[TestFixture]
public class ModerationAuthTests : TestBase
{
    [Test]
    public async Task NormalUser_Cannot_See_Edit_Or_Moderation_On_Others_Topic()
    {
        await LoginAsync("demo");
        // Mở chủ đề "Nội quy" do admin đăng (ghim đầu tu-van-bao-gia) — chắc chắn không phải của demo.
        await Page.GotoAsync("/danh-muc/nha-thau-bao-gia");
        await Page.Locator(".topic-card .topic-title").First.ClickAsync();
        await Page.WaitForSelectorAsync("article h1");
        var author = await Page.Locator("article .user-link[data-username]").First.GetAttributeAsync("data-username");

        Assert.That(author, Is.Not.EqualTo("demo"), "Chủ đề mở phải của người khác.");
        Assert.That(await Page.Locator("article [data-delete-topic]").CountAsync(), Is.EqualTo(0), $"Bài của @{author}: người dùng thường không được thấy nút Xóa.");
        Assert.That(await Page.Locator("article a[href$='/sua']").CountAsync(), Is.EqualTo(0), "Không được thấy link Sửa.");
        Assert.That(await Page.Locator("[data-menu=mod-menu]").CountAsync(), Is.EqualTo(0), "Người dùng thường không có menu kiểm duyệt.");
    }

    [Test]
    public async Task NormalUser_Cannot_Access_Moderation_Dashboard()
    {
        await LoginAsync("demo");
        await Page.GotoAsync("/kiem-duyet");
        Assert.That(Page.Url, Does.Contain("/tu-choi-truy-cap"));
    }

    [Test]
    public async Task Admin_Can_Access_Moderation_Dashboard()
    {
        await LoginAsync("admin");
        await Page.GotoAsync("/kiem-duyet");
        await Expect(Page.Locator("h1")).ToContainTextAsync("kiểm duyệt");
    }

    [Test]
    public async Task Admin_Can_Pin_Topic()
    {
        await LoginAsync("admin");
        await OpenCategoryTopicAsync("vat-lieu-xay-dung");

        await Page.ClickAsync("[data-menu=mod-menu]");
        await Page.ClickAsync("[data-mod=pin]");
        // topics.js tải lại trang sau khi ghim -> chờ flair "Đã ghim".
        await Expect(Page.Locator(".flair-pinned").First).ToBeVisibleAsync(new() { Timeout = 12000 });
    }

    [Test]
    public async Task Admin_Sees_Moderation_Menu_On_Topic()
    {
        await LoginAsync("admin");
        await OpenCategoryTopicAsync("ket-cau-thi-cong");
        await Expect(Page.Locator("[data-menu=mod-menu]")).ToBeVisibleAsync();
    }
}
