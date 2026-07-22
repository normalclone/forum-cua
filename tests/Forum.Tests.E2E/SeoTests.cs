using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Forum.Tests.E2E;

[TestFixture]
public class SeoTests : TestBase
{
    [Test]
    public async Task Home_Has_SiteName_Title()
    {
        await Page.GotoAsync("/");
        await Expect(Page).ToHaveTitleAsync(new Regex("Diễn đàn Cửa"));
    }

    [Test]
    public async Task TopicDetail_Has_Seo_Tags()
    {
        var url = await OpenFirstTopicAsync();

        // Đúng một thẻ H1 (tiêu đề chủ đề).
        Assert.That(await Page.Locator("h1").CountAsync(), Is.EqualTo(1), "Trang chủ đề phải có đúng 1 H1.");

        // Canonical đúng định dạng /chu-de/{id}/{slug}.
        var canonical = await Page.Locator("link[rel=canonical]").First.GetAttributeAsync("href");
        Assert.That(canonical, Does.Contain("/chu-de/"));

        // Meta description tồn tại.
        Assert.That(await Page.Locator("meta[name=description]").CountAsync(), Is.GreaterThanOrEqualTo(1));

        // Open Graph + Twitter.
        Assert.That(await Page.Locator("meta[property='og:title']").CountAsync(), Is.GreaterThanOrEqualTo(1));
        Assert.That(await Page.Locator("meta[name='twitter:card']").CountAsync(), Is.GreaterThanOrEqualTo(1));

        // JSON-LD DiscussionForumPosting/QAPage + BreadcrumbList.
        var ld = await Page.Locator("script[type='application/ld+json']").AllTextContentsAsync();
        var joined = string.Join("\n", ld);
        Assert.That(joined, Does.Contain("DiscussionForumPosting").Or.Contain("QAPage"));
        Assert.That(joined, Does.Contain("BreadcrumbList"));

        // URL slug hợp lệ.
        Assert.That(Regex.IsMatch(url, @"/chu-de/\d+/[a-z0-9\-]+"), Is.True, $"URL không đúng định dạng slug: {url}");
    }

    [Test]
    public async Task Slug_Mismatch_Redirects_To_Canonical()
    {
        var url = await OpenFirstTopicAsync();
        var id = Regex.Match(url, @"/chu-de/(\d+)/").Groups[1].Value;

        await Page.GotoAsync($"/chu-de/{id}/sai-slug-co-tinh-12345");
        Assert.That(Page.Url, Does.Not.Contain("sai-slug-co-tinh"));
        Assert.That(Page.Url, Does.Contain($"/chu-de/{id}/"));
    }

    [Test]
    public async Task Sitemap_And_Robots_Available()
    {
        var sitemap = await Page.APIRequest.GetAsync(ServerFixture.BaseUrl + "/sitemap.xml");
        Assert.That(sitemap.Status, Is.EqualTo(200));
        Assert.That(await sitemap.TextAsync(), Does.Contain("<urlset"));

        var robots = await Page.APIRequest.GetAsync(ServerFixture.BaseUrl + "/robots.txt");
        Assert.That(robots.Status, Is.EqualTo(200));
        Assert.That(await robots.TextAsync(), Does.Contain("Sitemap:"));
    }
}
