using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Forum.Tests.E2E;

[TestFixture]
public class AdminTests : TestBase
{
    [Test]
    public async Task Admin_Dashboard_Loads_For_Admin()
    {
        await LoginAsync("admin");
        await Page.GotoAsync("/quan-tri");
        await Expect(Page.Locator(".admin-nav")).ToBeVisibleAsync(new() { Timeout = 8000 });
        await Expect(Page.Locator(".stat-grid")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Admin_Panel_Denied_For_Moderator()
    {
        await LoginAsync("mod");                       // mod là staff nhưng KHÔNG phải admin
        await Page.GotoAsync("/quan-tri");
        await Expect(Page.Locator(".admin-nav")).ToHaveCountAsync(0);   // bị chặn → không có UI quản trị
    }

    [Test]
    public async Task Admin_Can_Create_Category()
    {
        await LoginAsync("admin");
        await Page.GotoAsync("/quan-tri/danh-muc");
        var name = Unique("DM Test ");
        await Page.FillAsync("#cat-name", name);
        await Page.ClickAsync("#cat-form button[type=submit]");
        await Page.WaitForURLAsync("**/quan-tri/danh-muc");
        await Expect(Page.Locator("#cat-table")).ToContainTextAsync(name, new() { Timeout = 8000 });
    }

    [Test]
    public async Task Admin_Can_Change_User_Role()
    {
        await LoginAsync("admin");
        await Page.GotoAsync("/quan-tri/nguoi-dung?q=demo");
        var row = Page.Locator("[data-user-row]")
            .Filter(new() { Has = Page.Locator("[data-username=demo]") }).First;
        var badge = row.Locator("[data-role-badge]");
        var select = row.Locator("select[data-set-role]");

        await select.SelectOptionAsync("Moderator");
        await Expect(badge).ToHaveTextAsync("Moderator", new() { Timeout = 8000 });

        await select.SelectOptionAsync("Member");     // hoàn trả
        await Expect(badge).ToHaveTextAsync("Member", new() { Timeout = 8000 });
    }

    [Test]
    public async Task Admin_Settings_Update_SiteName_Then_Revert()
    {
        await LoginAsync("admin");
        await Page.GotoAsync("/quan-tri/cau-hinh");
        await Page.FillAsync("input[name=SiteName]", "Cửa Test");
        await Page.ClickAsync("form[action='/quan-tri/cau-hinh'] button[type=submit]");
        await Page.WaitForURLAsync("**/quan-tri/cau-hinh");
        await Expect(Page.Locator(".app-header .brand")).ToContainTextAsync("Cửa Test", new() { Timeout = 8000 });

        await Page.FillAsync("input[name=SiteName]", "Diễn đàn Cửa");   // hoàn trả
        await Page.ClickAsync("form[action='/quan-tri/cau-hinh'] button[type=submit]");
        await Page.WaitForURLAsync("**/quan-tri/cau-hinh");
        await Expect(Page.Locator(".app-header .brand")).ToContainTextAsync("Diễn đàn Cửa");
    }

    [Test]
    public async Task Admin_Content_Topic_Action_Toggles()
    {
        await LoginAsync("admin");
        await Page.GotoAsync("/quan-tri/noi-dung");
        var id = await Page.Locator("[data-topic-row]").First.GetAttributeAsync("data-topic-row");
        var sel = $"[data-topic-row='{id}'] button[data-topic-act]";   // nút đầu = Ghim/Bỏ ghim
        var before = await Page.Locator(sel).First.GetAttributeAsync("data-act");
        var flipped = before == "pin" ? "unpin" : "pin";

        await Page.Locator(sel).First.ClickAsync();                     // → location.reload()
        await Expect(Page.Locator(sel).First).ToHaveAttributeAsync("data-act", flipped, new() { Timeout = 10000 });

        await Page.Locator(sel).First.ClickAsync();                     // hoàn trả
        await Expect(Page.Locator(sel).First).ToHaveAttributeAsync("data-act", before!, new() { Timeout = 10000 });
    }

    [Test]
    public async Task Admin_Delete_Then_Restore_Topic()
    {
        await LoginAsync("admin");
        await Page.GotoAsync("/quan-tri/noi-dung?gom-xoa=true");
        var id = await Page.Locator("[data-topic-row]").First.GetAttributeAsync("data-topic-row");
        Page.Dialog += async (_, d) => await d.AcceptAsync();           // xác nhận xóa

        await Page.Locator($"[data-topic-row='{id}'] [data-act=delete]").ClickAsync();
        await Expect(Page.Locator($"[data-topic-row='{id}'] .flag-chip.del")).ToBeVisibleAsync(new() { Timeout = 10000 });

        await Page.Locator($"[data-topic-row='{id}'] [data-act=restore]").ClickAsync();
        await Expect(Page.Locator($"[data-topic-row='{id}'] .flag-chip.del")).ToHaveCountAsync(0, new() { Timeout = 10000 });
    }

    [Test]
    public async Task Admin_Badge_Create_And_Award()
    {
        await LoginAsync("admin");
        await Page.GotoAsync("/quan-tri/huy-hieu");
        var name = Unique("HH Test ");
        await Page.FillAsync("#badge-name", name);
        await Page.ClickAsync("#badge-form button[type=submit]");
        await Page.WaitForURLAsync("**/quan-tri/huy-hieu");
        var row = Page.Locator("[data-badge-row]").Filter(new() { HasText = name }).First;
        await Expect(row).ToBeVisibleAsync(new() { Timeout = 8000 });

        Page.Dialog += async (_, d) => await d.AcceptAsync("demo");     // trao cho demo
        await row.Locator("[data-badge-award]").ClickAsync();
        await Expect(row.Locator("[data-badge-holders]")).ToHaveTextAsync("1", new() { Timeout = 8000 });
    }

    [Test]
    public async Task Admin_Export_Users_Csv()
    {
        await LoginAsync("admin");
        var info = await Page.EvaluateAsync<string>(
            "async () => { const r = await fetch('/quan-tri/xuat/nguoi-dung'); return r.status + '|' + (r.headers.get('content-type')||''); }");
        Assert.That(info, Does.Contain("200"));
        Assert.That(info, Does.Contain("text/csv"));
    }

    [Test]
    public async Task Category_Approval_Flow()
    {
        // 1) Admin tạo danh mục YÊU CẦU DUYỆT.
        var catName = Unique("KiemDuyet ");
        await LoginAsync("admin");
        await Page.GotoAsync("/quan-tri/danh-muc");
        await Page.FillAsync("#cat-name", catName);
        await Page.CheckAsync("#cat-approval");
        await Page.ClickAsync("#cat-form button[type=submit]");
        await Page.WaitForURLAsync("**/quan-tri/danh-muc");
        await Expect(Page.Locator("#cat-table")).ToContainTextAsync(catName, new() { Timeout = 8000 });

        // 2) Demo đăng bài trong danh mục đó → chờ duyệt (banner hiện cho tác giả).
        await LogoutAsync();
        await LoginAsync("demo");
        await Page.GotoAsync("/tao-chu-de");
        var title = Unique("Bai cho duyet ");
        await Page.FillAsync("input[name=Title]", title);
        await Page.SelectOptionAsync("select[name=CategoryId]", new SelectOptionValue { Label = catName });
        await Page.FillAsync("textarea[name=Body]", "Nội dung bài cần kiểm duyệt về cửa gỗ.");
        await Page.ClickAsync("[data-submit]");
        await Page.WaitForURLAsync(u => Regex.IsMatch(u, @"/chu-de/\d+/"), new() { Timeout = 15000 });
        var topicPath = new Uri(Page.Url).AbsolutePath;
        await Expect(Page.Locator(".pending-banner")).ToBeVisibleAsync(new() { Timeout = 8000 });

        // 3) Khách chưa đăng nhập KHÔNG xem được bài chờ duyệt (404).
        await LogoutAsync();
        var resp = await Page.GotoAsync(topicPath);
        Assert.That(resp!.Status, Is.EqualTo(404));

        // 4) Admin duyệt bài từ bảng kiểm duyệt.
        await LoginAsync("admin");
        await Page.GotoAsync("/kiem-duyet");
        var id = Regex.Match(topicPath, @"/chu-de/(\d+)/").Groups[1].Value;
        var row = Page.Locator($"[data-approval-row='{id}']");
        await Expect(row).ToBeVisibleAsync(new() { Timeout = 8000 });
        await row.Locator("[data-approve]").ClickAsync();
        await Expect(row).ToHaveCountAsync(0, new() { Timeout = 8000 });

        // 5) Sau khi duyệt: bài hiển thị công khai, hết banner.
        await LogoutAsync();
        var resp2 = await Page.GotoAsync(topicPath);
        Assert.That(resp2!.Status, Is.EqualTo(200));
        await Expect(Page.Locator("article h1")).ToContainTextAsync(title);
        await Expect(Page.Locator(".pending-banner")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Admin_Announcement_Shows_In_Ticker()
    {
        await LoginAsync("admin");
        await Page.GotoAsync("/quan-tri/thong-bao");
        var msg = Unique("TB test ");
        await Page.FillAsync("#ann-msg", msg);
        await Page.ClickAsync("#ann-form button[type=submit]");
        await Page.WaitForURLAsync("**/quan-tri/thong-bao");
        await Page.GotoAsync("/");
        await Expect(Page.Locator(".ticker")).ToContainTextAsync(msg, new() { Timeout = 8000 });
    }

    [Test]
    public async Task Admin_Cms_Page_Publishes()
    {
        await LoginAsync("admin");
        await Page.GotoAsync("/quan-tri/trang/sua");
        var slug = "test-" + System.Guid.NewGuid().ToString("N")[..8];
        var marker = "Marker-" + slug;
        await Page.FillAsync("input[name=Title]", "Trang test " + slug);
        await Page.FillAsync("input[name=Slug]", slug);
        await Page.FillAsync("textarea[name=Body]", "# " + marker + "\n\nNội dung trang test.");
        await Page.ClickAsync("form[action='/quan-tri/trang/luu'] button[type=submit]");
        await Page.WaitForURLAsync("**/quan-tri/trang");
        var resp = await Page.GotoAsync("/trang/" + slug);
        Assert.That(resp!.Status, Is.EqualTo(200));
        await Expect(Page.Locator("article")).ToContainTextAsync(marker);
    }

    [Test]
    public async Task Admin_Warn_User_Records_Warning()
    {
        await LoginAsync("admin");
        await Page.GotoAsync("/quan-tri/nguoi-dung?q=demo");
        await Page.Locator("[data-user-row]").Filter(new() { Has = Page.Locator("[data-username=demo]") })
            .First.Locator("a[href^='/quan-tri/nguoi-dung/']").ClickAsync();
        await Page.WaitForURLAsync(u => Regex.IsMatch(u, @"/quan-tri/nguoi-dung/\d+"));
        var reason = Unique("Canh cao ");
        Page.Dialog += async (_, d) => await d.AcceptAsync(reason);
        await Page.ClickAsync("[data-warn]");
        await Expect(Page.GetByText(reason)).ToBeVisibleAsync(new() { Timeout = 8000 });
    }

    [Test]
    public async Task Admin_Delete_Then_Restore_Comment()
    {
        await LoginAsync("admin");
        await Page.GotoAsync("/quan-tri/noi-dung/binh-luan");
        var id = await Page.Locator("[data-comment-row]").First.GetAttributeAsync("data-comment-row");
        Page.Dialog += async (_, d) => await d.AcceptAsync();
        await Page.Locator($"[data-comment-row='{id}'] [data-comment-del]").ClickAsync();
        await Expect(Page.Locator($"[data-comment-row='{id}']")).ToHaveCountAsync(0, new() { Timeout = 8000 });

        await Page.GotoAsync("/quan-tri/noi-dung/binh-luan?gom-xoa=true");
        var row = Page.Locator($"[data-comment-row='{id}']");
        await Expect(row.Locator(".flag-chip.del")).ToBeVisibleAsync(new() { Timeout = 8000 });
        await row.Locator("[data-comment-restore]").ClickAsync();
        await Expect(Page.Locator($"[data-comment-row='{id}'] .flag-chip.del")).ToHaveCountAsync(0, new() { Timeout = 10000 });
    }
}
