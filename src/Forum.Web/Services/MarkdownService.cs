using System.Text.RegularExpressions;
using Ganss.Xss;
using Markdig;

namespace Forum.Web.Services;

public interface IMarkdownService
{
    /// <summary>Render Markdown -&gt; HTML an toàn (đã sanitize, ảnh lazy-load).</summary>
    string ToHtml(string? markdown);

    /// <summary>Trích đoạn văn bản thuần (cho meta description, preview).</summary>
    string Excerpt(string? markdown, int maxLength = 160);

    /// <summary>URL ảnh đầu tiên trong nội dung (cho Open Graph), nếu có.</summary>
    string? FirstImageUrl(string? markdown);
}

public partial class MarkdownService : IMarkdownService
{
    private readonly MarkdownPipeline _pipeline;
    private readonly HtmlSanitizer _sanitizer;

    public MarkdownService()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()   // bảng, danh sách công việc, autolink, emphasis mở rộng...
            .UseAutoLinks()
            .DisableHtml()             // chặn HTML thô trong Markdown (an toàn XSS)
            .Build();

        _sanitizer = new HtmlSanitizer();
        _sanitizer.AllowedAttributes.Add("class");
        _sanitizer.AllowedAttributes.Add("loading");
        _sanitizer.AllowedAttributes.Add("id");
        _sanitizer.AllowedTags.Add("figure");
        _sanitizer.AllowedTags.Add("figcaption");
        // Cho phép thuộc tính rel/target trên link để mở tab mới an toàn.
        _sanitizer.AllowedAttributes.Add("target");
        _sanitizer.AllowedAttributes.Add("rel");
    }

    public string ToHtml(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return string.Empty;

        var html = Markdown.ToHtml(markdown, _pipeline);
        html = _sanitizer.Sanitize(html);

        // Đảm bảo chỉ có một H1 trên trang (tiêu đề chủ đề): hạ h1 trong nội dung -> h2.
        html = html.Replace("<h1", "<h2", StringComparison.OrdinalIgnoreCase)
                   .Replace("</h1>", "</h2>", StringComparison.OrdinalIgnoreCase);

        // Lazy-load + decoding async cho mọi ảnh (tối ưu tốc độ tải trang).
        html = ImgTag().Replace(html, "<img loading=\"lazy\" decoding=\"async\" ");

        // Link ngoài mở tab mới + rel an toàn.
        html = html.Replace("<a href=\"http", "<a target=\"_blank\" rel=\"noopener nofollow\" href=\"http");

        return html;
    }

    public string Excerpt(string? markdown, int maxLength = 160)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return string.Empty;

        var plain = Markdown.ToPlainText(markdown, _pipeline);
        plain = WhiteSpace().Replace(plain, " ").Trim();

        if (plain.Length <= maxLength)
            return plain;

        var cut = plain[..maxLength];
        var lastSpace = cut.LastIndexOf(' ');
        if (lastSpace > 60) cut = cut[..lastSpace];
        return cut.TrimEnd() + "…";
    }

    public string? FirstImageUrl(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return null;

        var m = MarkdownImage().Match(markdown);
        return m.Success ? m.Groups[1].Value : null;
    }

    [GeneratedRegex("<img ")]
    private static partial Regex ImgTag();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpace();

    [GeneratedRegex(@"!\[[^\]]*\]\(([^)\s]+)")]
    private static partial Regex MarkdownImage();
}
