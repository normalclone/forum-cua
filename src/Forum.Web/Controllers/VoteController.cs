using Forum.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Forum.Web.Controllers;

[Route("vote")]
public class VoteController : ForumControllerBase
{
    private readonly IVoteService _vote;
    public VoteController(IVoteService vote) => _vote = vote;

    public record VoteRequest(int Id, int Value);

    [HttpPost("topic")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Topic([FromBody] VoteRequest req)
    {
        try
        {
            var r = await _vote.VoteTopicAsync(req.Id, CurrentUserId, req.Value);
            return Json(new { score = r.Score, up = r.UpvoteCount, down = r.DownvoteCount, userVote = r.UserVote });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("comment")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Comment([FromBody] VoteRequest req)
    {
        try
        {
            var r = await _vote.VoteCommentAsync(req.Id, CurrentUserId, req.Value);
            return Json(new { score = r.Score, up = r.UpvoteCount, down = r.DownvoteCount, userVote = r.UserVote });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
