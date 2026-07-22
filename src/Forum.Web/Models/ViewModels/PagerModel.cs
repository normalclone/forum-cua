namespace Forum.Web.Models.ViewModels;

/// <summary>Dữ liệu phân trang cho partial _Pager (SEO-friendly: trang=1 không thêm query).</summary>
public class PagerModel
{
    public int Page { get; set; }
    public int TotalPages { get; set; }
    public string Path { get; set; } = "/";        // đường dẫn không kèm tham số trang
    public string? Query { get; set; }              // query khác (vd "sap-xep=moi"), không gồm "trang"

    public string Url(int page)
    {
        var hasQuery = !string.IsNullOrEmpty(Query);
        if (page <= 1)
            return hasQuery ? $"{Path}?{Query}" : Path;
        return hasQuery ? $"{Path}?{Query}&trang={page}" : $"{Path}?trang={page}";
    }
}
