using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Forum.Tests.E2E;

[TestFixture]
public class TopicTests : TestBase
{
    [Test]
    public async Task Create_Edit_Delete_Own_Topic()
    {
        await LoginAsync("demo");

        // ---- Tạo ----
        await Page.GotoAsync("/tao-chu-de");
        var title = Unique("Chủ đề kiểm thử cửa ");
        await Page.FillAsync("input[name=Title]", title);
        await Page.SelectOptionAsync("select[name=CategoryId]", new SelectOptionValue { Label = "Cửa gỗ" });
        await Page.FillAsync("textarea[name=Body]", "Nội dung kiểm thử về **cửa gỗ HDF** và cửa gỗ tự nhiên cho phòng ngủ.");
        await Page.FillAsync("[data-tag-add]", "kiểm-thử");
        await Page.PressAsync("[data-tag-add]", "Enter");
        await Page.ClickAsync("[data-submit]");

        await Page.WaitForURLAsync(u => Regex.IsMatch(u, @"/chu-de/\d+/"), new() { Timeout = 15000 });
        await Expect(Page.Locator("article h1")).ToContainTextAsync(title);
        var id = Regex.Match(Page.Url, @"/chu-de/(\d+)/").Groups[1].Value;

        // ---- Sửa ----
        await Page.GotoAsync($"/chu-de/{id}/sua");
        await Page.FillAsync("input[name=Title]", title + " (đã sửa)");
        await Page.ClickAsync("button:has-text('Lưu thay đổi')");
        await Page.WaitForURLAsync(u => Regex.IsMatch(u, @"/chu-de/\d+/"), new() { Timeout = 15000 });
        await Expect(Page.Locator("article h1")).ToContainTextAsync("đã sửa");

        // ---- Xóa (hộp thoại xác nhận) ----
        await Page.ClickAsync("[data-delete-topic]");
        await Page.ClickAsync(".modal [data-act=ok]");
        await Page.WaitForURLAsync(u => !u.Contains($"/chu-de/{id}/"), new() { Timeout = 15000 });

        // Chủ đề đã xóa -> 404.
        var resp = await Page.APIRequest.GetAsync($"{ServerFixture.BaseUrl}/chu-de/{id}/x");
        Assert.That(resp.Status, Is.EqualTo(404));
    }

    [Test]
    public async Task Create_Topic_Requires_Login()
    {
        await Page.GotoAsync("/tao-chu-de");
        // Chưa đăng nhập -> chuyển tới trang đăng nhập.
        Assert.That(Page.Url, Does.Contain("/dang-nhap"));
    }
}
