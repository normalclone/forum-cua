# CLAUDE.md — Diễn đàn Cửa

Ngữ cảnh cho Claude Code khi làm việc trong repo này. Đọc kèm: [README.md](README.md), [docs/DATABASE.md](docs/DATABASE.md), [docs/API.md](docs/API.md).

## Dự án là gì
Diễn đàn thảo luận chuyên về **CỬA** (cửa gỗ, nhôm kính, cuốn, uPVC, chống cháy, phụ kiện, phong thủy…), **ASP.NET Core MVC (.NET 8)**, **server-side rendering**, tối ưu **SEO**, giao diện trung tính kiểu Reddit/Discord, tiếng Việt, có dark mode + real-time (SignalR).

## Stack & layout
- .NET 8 (SDK pin `8.0.412` qua `global.json`). EF Core 8 + **SQLite** (trung lập provider — đổi `UseSqlite`→`UseSqlServer` là chuyển được). ASP.NET Core Identity (`IdentityUser<int>`). SignalR. Markdig + HtmlSanitizer. Bogus (seed). Playwright NUnit (E2E).
- `src/Forum.Web/` — 1 project MVC phân tầng: `Controllers/ Models{Entities,ViewModels,Seo} Data{Seed,Migrations} Services Hubs ViewComponents Helpers Views wwwroot`.
- `tests/Forum.Tests.E2E/` — Playwright E2E.
- `scripts/` — `run.ps1/.sh`, `test.ps1/.sh`. `docs/` — schema + API.

## Chạy & test (xem skill `run-app`, `e2e-test`)
- Chạy: `dotnet run --project src/Forum.Web --launch-profile http` → **http://localhost:5080**. Lần đầu tự migrate + seed (~15-20s).
- **Reseed sạch:** xóa `src/Forum.Web/forum.db*` rồi chạy lại (guard seed: nếu `Categories` đã có thì bỏ qua, nên phải xóa DB để seed lại).
- Test E2E: `dotnet build` → cài chromium `pwsh tests/Forum.Tests.E2E/bin/Debug/net8.0/playwright.ps1 install chromium` → `dotnet test`. **32 test, tất cả PASS.**

## Tài khoản demo (mật khẩu `Test@123`)
`admin` (Admin) · `mod` (Moderator) · `demo` (Member) + ~40 nhân vật trong `Data/Seed/content/_users.json`.

## ⚠️ Gotchas quan trọng
- **App đang chạy KHÓA `bin/Forum.Web.dll`** → `dotnet build`/`dotnet ef` sẽ lỗi MSB3027. **Luôn dừng app trước:** `taskkill //F //IM dotnet.exe` (Git Bash) rồi mới build / tạo migration / chạy test.
- **Static asset** (`wwwroot/css/*.css`, `wwwroot/js/*.js`) phục vụ từ thư mục project lúc chạy → **đổi xong chỉ cần refresh** (có `asp-append-version` cache-busting), KHÔNG cần rebuild/restart.
- **Razor (`.cshtml`) và C#** được biên dịch lúc build → đổi xong phải **rebuild + restart** app.
- **Migration tự áp dụng** khi khởi động (`Database.MigrateAsync()` trong `SeedService`). Thêm cột chỉ cần tạo migration; DB hiện có sẽ được nâng cấp, dữ liệu giữ nguyên (nhưng seed mới KHÔNG chạy lại — xóa DB nếu cần dữ liệu seed mới).
- E2E fixture tự khởi chạy DLL trên **http://127.0.0.1:5099** với DB riêng `forum-test.db` (xóa + seed lại mỗi lần → idempotent). Test chẩn đoán chụp ảnh là `[Explicit]` (không tính vào suite); chạy riêng: `dotnet test --filter Name=Capture_Screens`, ảnh lưu ở `shots/`.
- Bash tool giữ `cwd` giữa các lệnh — dùng đường dẫn tuyệt đối hoặc `cd /d/Code/forum &&` để tránh "Project file does not exist".

## Quy ước
- **URL slug tiếng Việt, SEO:** chủ đề `/chu-de/{id}/{slug}` (sai slug → 301 canonical), `/danh-muc/{slug}`, `/the/{slug}`, `/thanh-vien/{username}`. `/sitemap.xml` + `/robots.txt` động. SEO render qua `_Seo.cshtml` + `SeoService` (JSON-LD DiscussionForumPosting/QAPage + BreadcrumbList). Một `<h1>`/trang (Markdown hạ `h1→h2`).
- **Bình luận lồng nhau:** materialized path (`Comment.Path` + `Depth`), KHÔNG dùng HierarchyId. Xóa = **soft-delete** (`IsDeleted`). Mọi FK `DeleteBehavior.Restrict`.
- **Front-end:** vanilla JS (`window.Forum` trong `site.js`: toast/modal/AJAX/vote/dropdown/lightbox), CSS thuần. Icon = Lucide inline SVG qua `Html.Icon("name", size)` (`Helpers/IconHelper.cs`). Avatar/thời-gian qua `ViewHelpers`. Hover-card, autocomplete, board, chat-dock, compose là các module JS riêng nạp theo trang/toàn cục (xem `_Layout.cshtml`).
- **Design tokens:** `wwwroot/css/site.css` `:root` + `[data-theme="dark"]`. Một màu nhấn `--accent` (xanh), `--save` (vàng cho nút Lưu), upvote cam/downvote xanh. Animation ngắn 150–300ms trong `animations.css`, tôn trọng `prefers-reduced-motion`.
- **Real-time:** `ForumHub` (/hubs/forum: thông báo cá nhân group `user-{id}`, board newTopic broadcast, **shout** = Chat chung broadcast). `ChatHub` (/hubs/chat: chat 1-1 + presence + đính kèm). Đẩy từ service qua `IHubContext`.
- **Chat dock (Messenger):** tối đa 3 cửa sổ active (dàn ngang) + hàng chờ ≤5 avatar (FIFO). Logic trong `wwwroot/js/chat-dock.js`.

## Khi sửa giao diện
Hầu hết tinh chỉnh là **CSS/JS tĩnh** → sửa file trong `wwwroot/` rồi bảo người dùng refresh (không rebuild). Xác minh bằng Playwright + ảnh chụp (`shots/`) trước khi báo xong — người dùng ưu tiên kiểm chứng trực quan.
