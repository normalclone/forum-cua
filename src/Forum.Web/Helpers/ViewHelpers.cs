using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Forum.Web.Helpers;

public static class ViewHelpers
{
    private static readonly string[] AvatarColors =
    {
        "#e8590c", "#1c7ed6", "#2f9e44", "#9c36b5", "#c2255c", "#0c8599",
        "#e67700", "#5f3dc4", "#2b8a3e", "#a61e4d", "#1864ab", "#862e9c"
    };

    /// <summary>Avatar: dùng ảnh nếu có URL, ngược lại sinh avatar chữ cái (inline, không phụ thuộc mạng).</summary>
    public static IHtmlContent Avatar(this IHtmlHelper _, string? url, string? name, int size = 24, string? extraClass = null)
    {
        name ??= "?";
        var cls = $"avatar avatar-{size}" + (extraClass is null ? "" : $" {extraClass}");
        var alt = HtmlEncoder.Default.Encode(name);
        if (!string.IsNullOrEmpty(url))
            return new HtmlString($"<img class=\"{cls}\" src=\"{HtmlEncoder.Default.Encode(url)}\" alt=\"{alt}\" loading=\"lazy\" decoding=\"async\" width=\"{size}\" height=\"{size}\">");

        var initials = Initials(name);
        var color = AvatarColors[Math.Abs(StableHash(name)) % AvatarColors.Length];
        var fontSize = Math.Max(9, (int)(size * 0.42));
        var style = $"width:{size}px;height:{size}px;background:{color};color:#fff;display:inline-flex;" +
                    $"align-items:center;justify-content:center;font-weight:700;font-size:{fontSize}px;line-height:1;border-radius:50%;flex:none;";
        return new HtmlString($"<span class=\"{cls}\" style=\"{style}\" aria-label=\"{alt}\" title=\"{alt}\">{HtmlEncoder.Default.Encode(initials)}</span>");
    }

    private static string Initials(string name)
    {
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "?";
        if (parts.Length == 1) return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant();
        return ("" + parts[^2][0] + parts[^1][0]).ToUpperInvariant();
    }

    private static int StableHash(string s)
    {
        unchecked { int h = 23; foreach (var c in s) h = h * 31 + c; return h; }
    }

    /// <summary>Thời gian tương đối tiếng Việt: "vừa xong", "5 phút trước", "3 ngày trước"...</summary>
    public static string TimeAgo(DateTime utc)
    {
        var span = DateTime.UtcNow - utc;
        if (span.TotalSeconds < 45) return "vừa xong";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} phút trước";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours} giờ trước";
        if (span.TotalDays < 2) return "hôm qua";
        if (span.TotalDays < 30) return $"{(int)span.TotalDays} ngày trước";
        if (span.TotalDays < 365) return $"{(int)(span.TotalDays / 30)} tháng trước";
        return utc.ToLocalTime().ToString("dd/MM/yyyy");
    }

    public static string FullTime(DateTime utc) => utc.ToLocalTime().ToString("HH:mm dd/MM/yyyy");

    /// <summary>Rút gọn số lớn: 1234 -> "1,2k".</summary>
    public static string Compact(int n)
    {
        if (n < 1000) return n.ToString();
        if (n < 1_000_000) return (n / 1000.0).ToString("0.#").Replace('.', ',') + "k";
        return (n / 1_000_000.0).ToString("0.#").Replace('.', ',') + "tr";
    }

    public static string TradeLabel(UserTrade trade) => trade switch
    {
        UserTrade.ChuNha => "Chủ nhà",
        UserTrade.ThoLapDat => "Thợ lắp đặt",
        UserTrade.KienTrucSu => "Kiến trúc sư",
        UserTrade.DaiLy => "Đại lý / Nhà phân phối",
        UserTrade.KySuVatLieu => "Kỹ sư vật liệu",
        _ => "Thành viên"
    };
}
