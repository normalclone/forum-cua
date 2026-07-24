namespace Forum.Web.Models;

/// <summary>
/// Mô hình SEO cho mỗi trang. Controller điền vào ViewData["Seo"]; layout render
/// title, meta description, canonical, Open Graph, Twitter Card, rel prev/next và JSON-LD.
/// </summary>
public class SeoModel
{
    public string Title { get; set; } = "Diễn đàn Xây dựng Việt";
    public string? Description { get; set; }
    public string? CanonicalUrl { get; set; }

    public string OgType { get; set; } = "website";   // website | article
    public string? OgImage { get; set; }
    public string? OgImageAlt { get; set; }

    public string? PrevUrl { get; set; }              // rel="prev" (phân trang)
    public string? NextUrl { get; set; }              // rel="next"

    public bool NoIndex { get; set; }

    public DateTime? PublishedTime { get; set; }
    public DateTime? ModifiedTime { get; set; }
    public string? AuthorName { get; set; }

    /// <summary>Các khối JSON-LD thô (đã serialize) chèn vào &lt;head&gt;.</summary>
    public List<string> JsonLd { get; } = new();

    /// <summary>Breadcrumb (tên, url) để render điều hướng + JSON-LD BreadcrumbList.</summary>
    public List<BreadcrumbItem> Breadcrumbs { get; } = new();
}

public record BreadcrumbItem(string Name, string? Url);
