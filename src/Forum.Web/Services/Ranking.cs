namespace Forum.Web.Services;

/// <summary>Công thức xếp hạng "hot" kiểu Reddit (dùng cho sắp xếp xu hướng &amp; seed).</summary>
public static class Ranking
{
    private static readonly DateTime Epoch = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static double HotScore(int score, DateTime createdAtUtc)
    {
        var order = Math.Log10(Math.Max(Math.Abs(score), 1));
        var sign = score > 0 ? 1 : score < 0 ? -1 : 0;
        var seconds = (createdAtUtc - Epoch).TotalSeconds;
        return Math.Round(sign * order + seconds / 45000.0, 7);
    }
}
