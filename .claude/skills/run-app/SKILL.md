---
name: run-app
description: Build and run the Diễn đàn Cửa forum app locally on http://localhost:5080, optionally reseeding the database. Use when asked to run/start/restart the app, see a change live in the browser, or take a screenshot of the running site.
---

# Chạy app Diễn đàn Cửa

App: ASP.NET Core MVC (.NET 8) ở `src/Forum.Web`. Chạy tại **http://localhost:5080** (profile `http` trong `Properties/launchSettings.json`).

## Quy trình
1. **Dừng instance cũ trước khi build** (app đang chạy khóa `bin/Forum.Web.dll`, build sẽ lỗi MSB3027):
   ```bash
   taskkill //F //IM dotnet.exe 2>/dev/null
   ```
2. **(Tuỳ chọn) Reseed sạch** — xóa DB để seed lại từ đầu (guard seed bỏ qua nếu đã có dữ liệu):
   ```bash
   rm -f src/Forum.Web/forum.db*
   ```
   Khi KHÔNG xóa: migration mới vẫn tự áp dụng lúc khởi động, dữ liệu cũ giữ nguyên.
3. **Build + chạy** (chạy nền để không chặn):
   ```bash
   dotnet build src/Forum.Web/Forum.Web.csproj -v q --nologo
   dotnet run --project src/Forum.Web/Forum.Web.csproj --launch-profile http --no-build > /tmp/forum_run.log 2>&1 &
   ```
   Lần đầu (hoặc sau reseed) seed mất ~15-20s.
4. **Chờ sẵn sàng rồi xác nhận:**
   ```bash
   for i in $(seq 1 25); do code=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:5080/); [ "$code" = "200" ] && { echo READY; break; }; sleep 1; done
   ```

Hoặc dùng script: `./scripts/run.ps1` (PowerShell, `-Reset` để xóa DB) / `./scripts/run.sh` (`--reset`).

## Lưu ý
- Sửa **CSS/JS trong `wwwroot/`** → KHÔNG cần rebuild, chỉ refresh (cache-busting `asp-append-version`). Có thể curl `/css/site.css` để xác nhận bản mới được phục vụ.
- Sửa **`.cshtml` / C#** → phải dừng app, rebuild, chạy lại.
- Tài khoản demo: `admin` / `mod` / `demo`, mật khẩu `Test@123`.
- Để screenshot/kiểm chứng trực quan, ưu tiên dùng test chẩn đoán `[Explicit]` (xem skill `e2e-test`).
