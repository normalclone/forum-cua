using Microsoft.Extensions.Logging;

namespace Forum.Web.Services;

/// <summary>Gửi email. Bản dev ghi ra file/log để chạy được khi chưa có SMTP;
/// khi có SMTP thật chỉ cần thay bằng một lớp implement khác.</summary>
public interface IAppEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody);
    bool IsDevSink { get; }   // true = chưa gửi thật (chỉ ghi log) → UI có thể hiện link cho tiện test
}

/// <summary>Ghi email ra <c>logs/emails/</c> + ILogger. Không gửi ra ngoài.</summary>
public class LogEmailSender : IAppEmailSender
{
    private readonly ILogger<LogEmailSender> _log;
    private readonly string _dir;

    public LogEmailSender(ILogger<LogEmailSender> log, IWebHostEnvironment env)
    {
        _log = log;
        _dir = Path.Combine(env.ContentRootPath, "logs", "emails");
        Directory.CreateDirectory(_dir);
    }

    public bool IsDevSink => true;

    public async Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        var safeTo = string.Concat((toEmail ?? "no-addr").Split(Path.GetInvalidFileNameChars()));
        var file = Path.Combine(_dir, $"{stamp}_{safeTo}.html");
        var content = $"<!-- To: {toEmail} | Subject: {subject} | {DateTime.Now:u} -->\n<h3>{subject}</h3>\n{htmlBody}";
        await File.WriteAllTextAsync(file, content, System.Text.Encoding.UTF8);
        _log.LogInformation("[DEV EMAIL] To={To} Subject={Subject} -> {File}", toEmail, subject, file);
    }
}
