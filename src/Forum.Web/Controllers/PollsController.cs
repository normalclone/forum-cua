using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Forum.Web.Controllers;

[Route("binh-chon")]
public class PollsController : ForumControllerBase
{
    private readonly ApplicationDbContext _db;
    public PollsController(ApplicationDbContext db) => _db = db;

    public record VoteRequest(int OptionId);

    [HttpPost("vote")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Vote([FromBody] VoteRequest req)
    {
        var option = await _db.PollOptions.Include(o => o.Poll).FirstOrDefaultAsync(o => o.Id == req.OptionId);
        if (option is null) return NotFound(new { message = "Lựa chọn không tồn tại." });

        var pollId = option.PollId;
        var existing = await _db.PollVotes.FirstOrDefaultAsync(v => v.PollId == pollId && v.UserId == CurrentUserId);
        if (existing != null)
            return BadRequest(new { message = "Bạn đã bình chọn rồi." });

        _db.PollVotes.Add(new PollVote { PollId = pollId, PollOptionId = req.OptionId, UserId = CurrentUserId, CreatedAt = DateTime.UtcNow });
        option.VoteCount++;
        await _db.SaveChangesAsync();

        var options = await _db.PollOptions.Where(o => o.PollId == pollId)
            .OrderBy(o => o.DisplayOrder)
            .Select(o => new { o.Id, o.Text, o.VoteCount }).ToListAsync();
        var total = options.Sum(o => o.VoteCount);
        return Json(new { total, votedOptionId = req.OptionId, options });
    }
}
