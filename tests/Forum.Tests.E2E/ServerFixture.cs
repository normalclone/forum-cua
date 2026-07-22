using System.Diagnostics;

[assembly: LevelOfParallelism(1)]
[assembly: NonParallelizable]

namespace Forum.Tests.E2E;

/// <summary>
/// Khởi chạy ứng dụng web (đã build) trên cổng test với DB riêng (tạo mới mỗi lần
/// để idempotent), chờ sẵn sàng, và tắt sau khi chạy xong toàn bộ test.
/// </summary>
[SetUpFixture]
public class ServerFixture
{
    public const string BaseUrl = "http://127.0.0.1:5099";
    private static Process? _proc;

    [OneTimeSetUp]
    public async Task StartServerAsync()
    {
        var root = FindRepoRoot();
        var webDir = Path.Combine(root, "src", "Forum.Web");
        var dll = Path.Combine(webDir, "bin", "Debug", "net8.0", "Forum.Web.dll");
        if (!File.Exists(dll))
            throw new FileNotFoundException($"Chưa build ứng dụng web. Hãy 'dotnet build' trước. Thiếu: {dll}");

        // DB test mới để seed sạch (idempotent, chạy lại nhiều lần được).
        foreach (var f in Directory.GetFiles(webDir, "forum-test.db*"))
            try { File.Delete(f); } catch { /* ignore */ }

        var psi = new ProcessStartInfo("dotnet", $"\"{dll}\"")
        {
            WorkingDirectory = webDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        psi.Environment["ASPNETCORE_URLS"] = BaseUrl;
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        psi.Environment["ConnectionStrings__DefaultConnection"] = "Data Source=forum-test.db";

        _proc = Process.Start(psi) ?? throw new Exception("Không khởi chạy được tiến trình web.");
        _proc.OutputDataReceived += (_, _) => { };
        _proc.ErrorDataReceived += (_, _) => { };
        _proc.BeginOutputReadLine();
        _proc.BeginErrorReadLine();

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow.AddSeconds(150);
        while (DateTime.UtcNow < deadline)
        {
            if (_proc.HasExited) throw new Exception($"Web app thoát sớm (mã {_proc.ExitCode}).");
            try
            {
                var r = await http.GetAsync(BaseUrl + "/");
                if (r.IsSuccessStatusCode) return;
            }
            catch { /* chưa sẵn sàng */ }
            await Task.Delay(1000);
        }
        throw new TimeoutException("Web app không sẵn sàng trong thời gian chờ.");
    }

    [OneTimeTearDown]
    public void StopServer()
    {
        try
        {
            if (_proc is { HasExited: false })
            {
                _proc.Kill(entireProcessTree: true);
                _proc.WaitForExit(5000);
            }
        }
        catch { /* ignore */ }
        _proc?.Dispose();
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Forum.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new Exception("Không tìm thấy repo root (Forum.sln).");
    }
}
