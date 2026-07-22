using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Forum.Web.Controllers;

[Authorize]
[Route("tai-len")]
public class UploadController : ForumControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly ApplicationDbContext _db;

    // KHÔNG cho phép .svg: SVG có thể chứa <script>/handler và sẽ chạy như active content
    // trong origin của ứng dụng nếu mở trực tiếp → stored XSS. Chỉ nhận ảnh raster.
    private static readonly string[] AllowedImage = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    private static readonly string[] AllowedFile = { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".zip", ".txt" };
    private const long MaxBytes = 5 * 1024 * 1024; // 5MB

    public UploadController(IWebHostEnvironment env, ApplicationDbContext db)
    {
        _env = env; _db = db;
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile? file, string? altText)
    {
        if (file is null || file.Length == 0) return BadRequest(new { message = "Chưa chọn tệp." });
        if (file.Length > MaxBytes) return BadRequest(new { message = "Tệp vượt quá 5MB." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var isImage = AllowedImage.Contains(ext);
        if (!isImage && !AllowedFile.Contains(ext))
            return BadRequest(new { message = "Định dạng tệp không được hỗ trợ." });

        var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var dir = Path.Combine(webRoot, "uploads");
        Directory.CreateDirectory(dir);

        var stored = $"{Guid.NewGuid():N}{ext}";
        var path = Path.Combine(dir, stored);
        await using (var fs = System.IO.File.Create(path))
            await file.CopyToAsync(fs);

        var url = $"/uploads/{stored}";
        _db.Attachments.Add(new Attachment
        {
            UploaderId = CurrentUserId,
            FileName = Path.GetFileName(file.FileName),
            StoredPath = url,
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            IsImage = isImage,
            AltText = altText,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        return Json(new { url, isImage, name = Path.GetFileName(file.FileName) });
    }
}
