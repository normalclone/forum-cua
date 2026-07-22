using System.Text.RegularExpressions;

namespace Forum.Web.Services;

/// <summary>Bộ lọc từ ngữ &amp; chống spam cơ bản cho nội dung người dùng.
/// Danh sách từ cấm lấy từ <see cref="ISiteSettingService"/> (chỉnh được ở /quan-tri/cau-hinh).</summary>
public interface IWordFilterService
{
    bool ContainsBannedWord(string? text);
    string Mask(string? text);
    bool LooksLikeSpam(string? text);
}

public partial class WordFilterService : IWordFilterService
{
    private readonly ISiteSettingService _settings;

    public WordFilterService(ISiteSettingService settings) => _settings = settings;

    public bool ContainsBannedWord(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var regex = _settings.BannedRegex;
        return regex is not null && regex.IsMatch(TextNormalize.ForMatch(text));
    }

    public string Mask(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text ?? string.Empty;
        foreach (var w in _settings.BannedWords)
            text = Regex.Replace(text, Regex.Escape(w), new string('*', w.Length), RegexOptions.IgnoreCase);
        return text;
    }

    public bool LooksLikeSpam(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        // Quá nhiều liên kết.
        if (LinkPattern().Matches(text).Count >= 5) return true;

        // Lặp ký tự bất thường (vd "aaaaaaaaa").
        if (RepeatedChar().IsMatch(text)) return true;

        // Toàn chữ HOA và dài.
        var letters = text.Where(char.IsLetter).ToArray();
        if (letters.Length > 30 && letters.All(char.IsUpper)) return true;

        return false;
    }

    [GeneratedRegex(@"https?://", RegexOptions.IgnoreCase)]
    private static partial Regex LinkPattern();

    [GeneratedRegex(@"(.)\1{9,}")]
    private static partial Regex RepeatedChar();
}
