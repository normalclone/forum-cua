using Forum.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Forum.Web.Controllers;

[Route("markdown")]
public class MarkdownController : ForumControllerBase
{
    private readonly IMarkdownService _md;
    public MarkdownController(IMarkdownService md) => _md = md;

    public record PreviewRequest(string? Markdown);

    [HttpPost("preview")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public IActionResult Preview([FromBody] PreviewRequest req)
        => Json(new { html = _md.ToHtml(req.Markdown) });
}
