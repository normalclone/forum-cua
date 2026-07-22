using Microsoft.Playwright;

namespace Forum.Tests.E2E;

[TestFixture]
public class ChatTests : TestBase
{
    // PNG 1x1 hợp lệ.
    private static byte[] Png() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    private async Task OpenDockConversationAsync()
    {
        await Page.ClickAsync("#chat-launcher");
        await Page.WaitForSelectorAsync(".chat-list-panel .chat-list-item");
        await Page.Locator(".chat-list-panel .chat-list-item").First.ClickAsync();
        await Page.WaitForSelectorAsync(".chat-window .cw-text");
    }

    [Test]
    public async Task Send_Text_Message_In_Dock()
    {
        await LoginAsync("demo");
        await Page.GotoAsync("/");
        await OpenDockConversationAsync();
        var text = Unique("Xin chào ");
        await Page.FillAsync(".chat-window .cw-text", text);
        await Page.Locator(".chat-window .cw-text").PressAsync("Enter");
        await Expect(Page.Locator(".chat-window .cw-body")).ToContainTextAsync(text, new() { Timeout = 10000 });
    }

    [Test]
    public async Task Upload_Image_Then_View_In_Lightbox()
    {
        await LoginAsync("demo");
        await Page.GotoAsync("/");
        await OpenDockConversationAsync();

        await Page.Locator(".chat-window .cw-file").SetInputFilesAsync(new FilePayload
        {
            Name = "anh-cua.png", MimeType = "image/png", Buffer = Png()
        });

        await Expect(Page.Locator(".chat-window .chat-img").First).ToBeVisibleAsync(new() { Timeout = 12000 });
        await Page.Locator(".chat-window .chat-img").First.ClickAsync();
        await Expect(Page.Locator(".lightbox")).ToBeVisibleAsync(new() { Timeout = 5000 });
    }

    [Test]
    public async Task NormalUser_File_Over_1MB_Is_Rejected()
    {
        await LoginAsync("demo");
        await Page.GotoAsync("/");
        await OpenDockConversationAsync();

        await Page.Locator(".chat-window .cw-file").SetInputFilesAsync(new FilePayload
        {
            Name = "bao-gia-lon.pdf", MimeType = "application/pdf", Buffer = new byte[1_200_000]
        });

        await Expect(Page.Locator(".toast")).ToContainTextAsync("1MB", new() { Timeout = 8000 });
    }

    [Test]
    public async Task Global_Shoutbox_Send_Appears()
    {
        await LoginAsync("demo");
        await Page.GotoAsync("/");
        await Page.WaitForSelectorAsync("#shoutbox-form");
        var text = Unique("Chào cả nhà ");
        await Page.FillAsync("#shoutbox-input", text);
        await Page.Locator("#shoutbox-form button[type=submit]").ClickAsync();
        await Expect(Page.Locator("#shoutbox-msgs")).ToContainTextAsync(text, new() { Timeout = 10000 });
    }

    [Test]
    public async Task Staff_Can_Upload_File_Over_1MB()
    {
        await LoginAsync("admin");
        await Page.GotoAsync("/thanh-vien/demo");
        await Page.WaitForSelectorAsync("[data-chat-with]");
        await Page.Locator("[data-chat-with]").First.ClickAsync();
        await Page.WaitForSelectorAsync(".chat-window .cw-text");

        await Page.Locator(".chat-window .cw-file").SetInputFilesAsync(new FilePayload
        {
            Name = "bao-gia-2026.pdf", MimeType = "application/pdf", Buffer = new byte[1_200_000]
        });

        await Expect(Page.Locator(".chat-window .chat-file").First).ToBeVisibleAsync(new() { Timeout = 12000 });
    }
}
