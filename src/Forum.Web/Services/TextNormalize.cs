using System.Globalization;
using System.Text;

namespace Forum.Web.Services;

/// <summary>Chuẩn hóa văn bản tiếng Việt để khớp (bỏ dấu, đ→d, thường, gộp khoảng trắng).</summary>
public static class TextNormalize
{
    public static string ForMatch(string s)
    {
        var decomposed = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        // đ/Đ không phân rã qua FormD → thay thủ công sau khi hạ chữ thường.
        var cleaned = sb.ToString().ToLowerInvariant().Replace('đ', 'd');
        return string.Join(' ', cleaned.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
