using Forum.Web.Helpers;
using Forum.Web.Models;
using Forum.Web.Services;
using Microsoft.AspNetCore.SignalR;

namespace Forum.Web.Hubs;

/// <summary>
/// Hub chung: đẩy thông báo cá nhân (group user-{id}), cập nhật board (broadcast),
/// và "Chat chung" toàn diễn đàn (shoutbox, broadcast tới tất cả).
/// </summary>
public class ForumHub : Hub
{
    private readonly ApplicationDbContext _db;
    private readonly ISiteSettingService _settings;
    public ForumHub(ApplicationDbContext db, ISiteSettingService settings)
    {
        _db = db; _settings = settings;
    }

    public override async Task OnConnectedAsync()
    {
        var uid = Context.User?.UserId() ?? 0;
        if (uid > 0)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{uid}");
        await base.OnConnectedAsync();
    }

    /// <summary>Gửi tin vào Chat chung; phát tới mọi người đang online.</summary>
    public async Task SendShout(string body)
    {
        if (Context.User?.Identity?.IsAuthenticated != true) return;
        // Chat chung nằm chung công tắc với chat 1-1: tắt "chat" thì cấm luôn shout.
        if (!_settings.GetBool(SettingKeys.FeatureChat, true)) return;
        body = (body ?? "").Trim();
        if (body.Length == 0 || body.Length > 500) return;

        var uid = Context.User.UserId();
        var now = DateTime.UtcNow;
        var msg = new ShoutMessage { SenderId = uid, Body = body, CreatedAt = now };
        _db.ShoutMessages.Add(msg);
        await _db.SaveChangesAsync();

        var sender = await _db.Users.FindAsync(uid);
        await Clients.All.SendAsync("shout", new
        {
            id = msg.Id,
            senderId = uid,
            name = sender?.DisplayName ?? "",
            username = sender?.UserName,
            avatar = sender?.AvatarUrl,
            body,
            createdAt = now
        });
    }
}
