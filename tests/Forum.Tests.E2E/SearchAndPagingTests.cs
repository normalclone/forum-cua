using Microsoft.Playwright;

namespace Forum.Tests.E2E;

[TestFixture]
public class SearchAndPagingTests : TestBase
{
    [Test]
    public async Task Search_By_Keyword_Returns_Results()
    {
        await Page.GotoAsync("/tim-kiem?q=" + Uri.EscapeDataString("cửa"));
        await Expect(Page.Locator(".topic-card").First).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Test]
    public async Task Search_Filter_By_Category()
    {
        await Page.GotoAsync("/tim-kiem?q=" + Uri.EscapeDataString("cửa") + "&danh-muc=cua-nhom-kinh");
        await Expect(Page.Locator(".topic-card").First).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Test]
    public async Task Category_Page_Loads()
    {
        await Page.GotoAsync("/danh-muc/cua-nhom-kinh");
        await Expect(Page.Locator("h1")).ToContainTextAsync("nhôm");
        await Expect(Page.Locator(".topic-card").First).ToBeVisibleAsync();
    }

    [Test]
    public async Task Home_Pagination_Works()
    {
        await Page.GotoAsync("/?trang=2");
        await Expect(Page.Locator(".pager .current")).ToContainTextAsync("2");
    }

    [Test]
    public async Task Tag_Page_Loads()
    {
        await Page.GotoAsync("/the/cua-go");
        await Expect(Page.Locator(".topic-card").First).ToBeVisibleAsync(new() { Timeout = 10000 });
    }
}
