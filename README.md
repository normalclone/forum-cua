# Diễn đàn Cửa — Forum cộng đồng về CỬA (ASP.NET Core MVC, .NET 8)

Ứng dụng diễn đàn thảo luận chuyên về **cửa và vật liệu cửa** (cửa gỗ, nhôm kính, cửa cuốn, uPVC, cửa thép/chống cháy, phụ kiện, báo giá, phong thủy…), xây dựng bằng **ASP.NET Core MVC (.NET 8)** với **server-side rendering** và **tối ưu SEO** cho từng chủ đề.

Giao diện theo phong cách **đậm đặc thông tin (information-dense)**, tông màu **trung tính** kiểu Reddit/Discord, có **dark mode**, animation tinh tế (tôn trọng `prefers-reduced-motion`), và nhiều tương tác real-time (SignalR).

> **Tình trạng:** Chạy được hoàn chỉnh tất cả luồng. **32/32 test E2E (Playwright) PASS.**

---

## 1. Công nghệ

| Thành phần | Lựa chọn |
|---|---|
| Web framework | ASP.NET Core MVC (.NET 8), Razor Views (SSR) |
| ORM / DB | EF Core 8 + **SQLite** (trung lập provider — đổi sang SQL Server dễ dàng) |
| Xác thực | ASP.NET Core Identity (`IdentityUser<int>`, vai trò Admin/Moderator/Member) |
| Real-time | SignalR (thông báo, board, chat 1-1) |
| Markdown | Markdig + HtmlSanitizer (Ganss.Xss) |
| Seed | Bogus (số liệu/ngày) + nội dung tiếng Việt viết tay theo ngành cửa |
| Front-end | Vanilla JS (không SPA), CSS thuần, icon Lucide (inline SVG) |
| Test E2E | Microsoft.Playwright.NUnit |

## 2. Yêu cầu môi trường

- **.NET SDK 8.0** (pin trong `global.json`). Có thể cài SDK 9 song song.
- **Node.js** (chỉ để Playwright tải trình duyệt) hoặc PowerShell.
- Không cần cài database server — SQLite chạy ngay, file `forum.db` tạo trong thư mục dự án.

## 3. Chạy nhanh

```bash
# từ thư mục gốc repo
dotnet build
dotnet run --project src/Forum.Web
```

Mở trình duyệt tại **http://localhost:5080**.

### 3b. Chạy demo online bằng GitHub Codespaces

Repo đã có sẵn `.devcontainer/` để chạy trực tiếp trên cloud của GitHub (không cần cài .NET ở máy):

1. Trên trang repo GitHub → nút **Code** (xanh) → tab **Codespaces** → **Create codespace on main**.
2. Chờ container khởi tạo (`dotnet restore` chạy tự động), rồi trong terminal chạy:
   ```bash
   dotnet run --project src/Forum.Web/Forum.Web.csproj --no-launch-profile
   ```
3. Lần đầu tự migrate + seed (~15–20s). Codespaces sẽ hiện thông báo forward **cổng 5080** → bấm **Open in Browser**.
4. Để chia sẻ **link demo công khai**: tab **Ports** → chuột phải cổng 5080 → **Port Visibility → Public**, rồi copy URL (dạng `https://<tên>-5080.app.github.dev`).

> Link chỉ hoạt động khi codespace đang bật. Tài khoản demo: `admin` / `mod` / `demo` (mật khẩu `Test@123`).

- **Lần chạy đầu** tự động tạo DB + đổ dữ liệu mẫu (~15–20 giây): 43 người dùng, 9 danh mục, 73 chủ đề, ~480 bình luận lồng nhau, ~100 thẻ, poll, báo cáo, hội thoại chat…
- **Đổ lại dữ liệu sạch:** xóa `src/Forum.Web/forum.db*` rồi chạy lại.

Hoặc dùng script tiện lợi:

```bash
# Windows PowerShell
./scripts/run.ps1            # build + (tùy chọn) reset DB + chạy
./scripts/run.ps1 -Reset     # xóa DB và seed lại từ đầu

# Bash
./scripts/run.sh             # build + chạy
./scripts/run.sh --reset     # reset DB + seed lại
```

### Tài khoản demo (mật khẩu chung: `Test@123`)

| Tài khoản | Vai trò | Ghi chú |
|---|---|---|
| `admin` | **Admin** | Toàn quyền kiểm duyệt |
| `mod` | **Moderator** | Ghim/khóa/xóa, xử lý báo cáo |
| `demo` | Member | Tài khoản dùng thử (có sẵn thông báo, bookmark, hội thoại chat) |

Ngoài ra có ~40 thành viên mẫu (chủ nhà, thợ lắp đặt, kiến trúc sư, đại lý, kỹ sư vật liệu) — danh sách trong `src/Forum.Web/Data/Seed/content/_users.json`, tất cả dùng mật khẩu `Test@123`.

## 4. Tính năng

**Cốt lõi:** đăng ký/đăng nhập/đăng xuất • hồ sơ cá nhân (avatar, tiểu sử, vai trò ngành) • tạo/sửa/xóa chủ đề (Markdown, tag dạng chip, tự lưu nháp, cảnh báo khi rời trang) • bình luận lồng nhau (thread line) + trả lời chèn `@mention` • upvote/downvote (AJAX, hot score) • tìm kiếm + lọc danh mục/thẻ/thời gian + autocomplete • bookmark & theo dõi chủ đề • bộ lọc từ ngữ/chống spam cơ bản.

**Tương tác:** thông báo (chuông + badge, @mention/trả lời) • theo dõi người dùng • **hover card** xem hồ sơ (delay, định vị thông minh, fade+scale, cache AJAX, hỗ trợ bàn phím) • dấu "đã chỉnh sửa" • báo cáo nội dung • toast/confirm cho mọi thao tác.

**Bổ sung:** **poll/bình chọn** trong chủ đề • **tải file/ảnh đính kèm** (alt text) • **huy hiệu + điểm uy tín + bảng xếp hạng** • **chat 1-1 real-time** + trạng thái online • bảng tin cá nhân • dark mode (lưu lựa chọn) • nút đăng nhập mạng xã hội (UI sẵn, bật khi cấu hình OAuth).

**Kiểm duyệt:** Admin/Mod ghim/khóa/nổi bật/di chuyển/xóa chủ đề • hàng đợi báo cáo • **nhật ký kiểm duyệt (audit log)**.

**Real-time (SignalR):** đẩy thông báo cá nhân (rung chuông) • chủ đề mới hiện banner trên board • chat tin nhắn tức thì + presence.

## 5. SEO — cách triển khai

| Yếu tố | Triển khai |
|---|---|
| **SSR** | Toàn bộ nội dung render phía server bằng Razor; AJAX chỉ cho tương tác nhẹ. |
| **URL slug thân thiện** | `/chu-de/{id}/{slug}` (vd `/chu-de/17/cua-nhom-kinh-co-chong-duoc-bao-khong`). `SlugService` bỏ dấu tiếng Việt, xử lý `đ→d`. |
| **301 canonical** | Mở sai slug → **301** về URL chuẩn (`TopicsController.Detail`). |
| **Meta động** | `_Seo.cshtml` render `<title>`, `meta description`, `canonical` cho mỗi trang qua `SeoModel`. |
| **Open Graph & Twitter Card** | Đầy đủ `og:*`, `twitter:card` (ảnh đầu tiên trong bài làm `og:image`). |
| **JSON-LD Schema.org** | `DiscussionForumPosting` / `QAPage` (tùy chủ đề hỏi-đáp) + `BreadcrumbList` + `WebSite` (`SeoService`). |
| **Breadcrumb** | Điều hướng hiển thị (`_Breadcrumbs.cshtml`) đồng bộ với JSON-LD `BreadcrumbList`. |
| **Một H1/trang** | Tiêu đề chủ đề là H1 duy nhất; Markdown trong bài tự hạ `h1→h2`. |
| **Sitemap & robots** | `/sitemap.xml` sinh động (home/danh mục/chủ đề/thẻ) + `/robots.txt` trỏ sitemap. |
| **Phân trang** | `rel="prev"/"next"` + canonical hợp lý; `?trang=N` (trang 1 không thêm query). |
| **Hiệu năng & ảnh** | Ảnh `loading="lazy"` + `decoding="async"` + alt text; CSS/JS tách file, animation thuần CSS. |

## 6. Cấu trúc dự án

```
forum/
├─ Forum.sln, global.json, .config/dotnet-tools.json
├─ README.md, docs/{DATABASE.md, API.md}
├─ scripts/{run.ps1, run.sh, test.ps1, test.sh}
├─ src/Forum.Web/
│  ├─ Controllers/        # Home, Account, Profile, Topics, Comments, Vote, Polls,
│  │                      # Categories, Tags, Search, Notifications, Moderation,
│  │                      # Members, Upload, Sitemap, Markdown
│  ├─ Models/             # Entities/  ViewModels/  Enums.cs  PagedResult.cs  Seo/
│  ├─ Data/               # ApplicationDbContext, Migrations/, Seed/ (SeedService + content JSON)
│  ├─ Services/           # Slug, Markdown, Seo, ForumUrl, WordFilter, Notification,
│  │                      # Reputation, Vote, Search, Moderation, Topic, Comment, Ranking
│  ├─ Hubs/               # ForumHub (thông báo/board), ChatHub (chat 1-1 + presence)
│  ├─ ViewComponents/     # NotificationBell, ForumSidebar
│  ├─ Helpers/            # IconHelper, ViewHelpers, ClaimsExtensions, ControllerExtensions
│  ├─ Views/              # Razor views + Shared partials
│  └─ wwwroot/            # css/, js/, lib/, uploads/
└─ tests/Forum.Tests.E2E/ # Playwright NUnit: ServerFixture + 7 test files
```

Xem **[docs/DATABASE.md](docs/DATABASE.md)** (lược đồ CSDL) và **[docs/API.md](docs/API.md)** (danh sách endpoint).

## 7. Kiểm thử E2E (Playwright)

```bash
# 1. Build (tạo cả script cài trình duyệt)
dotnet build

# 2. Cài trình duyệt Chromium cho Playwright (chỉ làm 1 lần)
pwsh tests/Forum.Tests.E2E/bin/Debug/net8.0/playwright.ps1 install chromium
#  (Windows không có pwsh:  powershell -File tests/Forum.Tests.E2E/bin/Debug/net8.0/playwright.ps1 install chromium)

# 3. Chạy toàn bộ test
dotnet test
```

Hoặc một lệnh: `./scripts/test.ps1` (PowerShell) / `./scripts/test.sh` (bash) — tự build, cài trình duyệt, chạy test.

**Cơ chế:** `ServerFixture` tự khởi chạy app đã build trên `http://127.0.0.1:5099` với **DB riêng `forum-test.db`** (xóa & seed lại mỗi lần ⇒ idempotent, chạy lại nhiều lần được), chờ sẵn sàng rồi chạy test, và tắt app khi xong. Test chạy tuần tự (không song song) để tránh tranh chấp dữ liệu.

**Bao phủ (32 test):** đăng ký→đăng nhập→đăng xuất • tạo/sửa/xóa chủ đề (tag, nháp) • xem chi tiết • bình luận lồng nhau • upvote chủ đề & bình luận • tìm kiếm + lọc danh mục • phân trang • bookmark • thông báo seed + **@mention liên người dùng** • **hover card** • dark mode (lưu sau reload) • **phân quyền** (người thường không sửa/xóa bài người khác, không vào được trang kiểm duyệt) • **kiểm duyệt** (admin ghim được, thấy menu kiểm duyệt) • **SEO** (title, canonical, meta description, OG/Twitter, JSON-LD DiscussionForumPosting+BreadcrumbList, slug đúng định dạng, **301** khi sai slug) • sitemap.xml & robots.txt.

```
Total tests: 32   Passed: 32   Failed: 0
```

## 8. Chuyển sang SQL Server

Tầng dữ liệu trung lập provider (không dùng tính năng đặc thù SQLite; bình luận lồng nhau dùng *materialized path* thay vì `HierarchyId`; mọi quan hệ đặt `DeleteBehavior.Restrict`). Để chuyển:

1. Thêm package `Microsoft.EntityFrameworkCore.SqlServer`.
2. Trong `Program.cs` đổi `options.UseSqlite(...)` → `options.UseSqlServer(...)`.
3. Đổi `ConnectionStrings:DefaultConnection` trong `appsettings.json`.
4. Tạo lại migration cho SQL Server (`dotnet ef migrations add InitialCreate`) hoặc dùng `EnsureCreated`.

## 9. Đăng nhập mạng xã hội (Google/Facebook)

Nút đã có sẵn trên trang đăng nhập/đăng ký. Để kích hoạt: điền `Authentication:Google` / `Authentication:Facebook` trong `appsettings.json` và thêm `.AddGoogle()/.AddFacebook()` trong `Program.cs`. (Mặc định để trống nên dùng đăng nhập nội bộ qua Identity.)
