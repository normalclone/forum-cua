using Microsoft.Playwright;

namespace Forum.Tests.E2E;

[TestFixture]
public class CommentVoteTests : TestBase
{
    [Test]
    public async Task Post_Comment_And_Nested_Reply()
    {
        await LoginAsync("demo");
        await OpenCategoryTopicAsync("ket-cau-thi-cong");

        var body = Unique("Bình luận kiểm thử ");
        await Page.FillAsync("[data-comment-form] textarea[name=body]", body);
        await Page.Locator("[data-comment-form] button[type=submit]").First.ClickAsync();
        await Expect(Page.Locator("#comment-tree")).ToContainTextAsync(body, new() { Timeout = 10000 });

        // Trả lời lồng vào bình luận vừa tạo (nút reply cuối cùng).
        await Page.Locator("[data-reply]").Last.ClickAsync();
        var reply = Unique("Trả lời lồng ");
        await Page.Locator(".reply-form textarea[name=body]").Last.FillAsync(reply);
        await Page.Locator(".reply-form button[type=submit]").Last.ClickAsync();
        await Expect(Page.Locator("#comment-tree")).ToContainTextAsync(reply, new() { Timeout = 10000 });

        // Bình luận lồng phải nằm trong .comment-children (có thread line).
        Assert.That(await Page.Locator(".comment-children .comment-body").CountAsync(), Is.GreaterThan(0));
    }

    [Test]
    public async Task Upvote_Topic_Updates_State()
    {
        await LoginAsync("demo");
        await OpenCategoryTopicAsync("ket-cau-thi-cong");

        var up = Page.Locator(".vote-rail .vote-btn.up");
        await up.ClickAsync();
        await Expect(up).ToHaveClassAsync(new System.Text.RegularExpressions.Regex(@"\bon\b"), new() { Timeout = 8000 });
    }

    [Test]
    public async Task Upvote_Comment_Updates_State()
    {
        await LoginAsync("demo");
        // Mở một chủ đề có sẵn bình luận (tránh tu-van-bao-gia vì chủ đề ghim "Nội quy" bị khóa).
        await OpenCategoryTopicAsync("chong-tham-son");
        var firstUp = Page.Locator(".comment-actions .vote-btn.up").First;
        if (await firstUp.CountAsync() == 0)
        {
            // Nếu chủ đề chưa có bình luận, tạo một bình luận để vote.
            await Page.FillAsync("[data-comment-form] textarea[name=body]", Unique("Để vote "));
            await Page.Locator("[data-comment-form] button[type=submit]").First.ClickAsync();
            await Page.WaitForTimeoutAsync(800);
            firstUp = Page.Locator(".comment-actions .vote-btn.up").First;
        }
        await firstUp.ClickAsync();
        await Expect(firstUp).ToHaveClassAsync(new System.Text.RegularExpressions.Regex(@"\bon\b"), new() { Timeout = 8000 });
    }
}
