using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Forum.Web.ViewComponents;

/// <summary>Box "Chat chung" toàn diễn đàn — hiển thị các tin gần nhất.</summary>
public class ShoutboxViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _db;
    public ShoutboxViewComponent(ApplicationDbContext db) => _db = db;

    public async Task<IViewComponentResult> InvokeAsync(int take = 25)
    {
        var msgs = await _db.ShoutMessages
            .Include(m => m.Sender)
            .OrderByDescending(m => m.CreatedAt)
            .Take(take)
            .ToListAsync();
        msgs.Reverse(); // cũ -> mới để hiển thị
        return View(msgs);
    }
}
