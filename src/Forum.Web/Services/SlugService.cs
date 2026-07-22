using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Forum.Web.Services;

public interface ISlugService
{
    /// <summary>Sinh slug thân thiện URL từ chuỗi tiếng Việt (bỏ dấu, đ-&gt;d, gạch nối).</summary>
    string Generate(string text, int maxLength = 80);
}

public partial class SlugService : ISlugService
{
    public string Generate(string text, int maxLength = 80)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "noi-dung";

        text = text.Trim().ToLowerInvariant();

        // 'đ' không tách dấu khi normalize nên xử lý riêng.
        text = text.Replace('đ', 'd');

        // Tách tổ hợp dấu rồi loại bỏ các dấu thanh/dấu phụ tiếng Việt.
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        text = sb.ToString().Normalize(NormalizationForm.FormC);

        // Mọi ký tự không phải a-z0-9 -> gạch nối, gộp gạch nối, cắt 2 đầu.
        text = NonAlphaNumeric().Replace(text, "-");
        text = MultiDash().Replace(text, "-").Trim('-');

        if (text.Length > maxLength)
            text = text[..maxLength].Trim('-');

        return string.IsNullOrEmpty(text) ? "noi-dung" : text;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphaNumeric();

    [GeneratedRegex("-+")]
    private static partial Regex MultiDash();
}
