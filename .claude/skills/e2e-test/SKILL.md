---
name: e2e-test
description: Build, install the Playwright browser, and run the Diễn đàn Cửa end-to-end test suite (NUnit + Playwright). Use when asked to run tests, verify flows, check nothing regressed, or capture verification screenshots of the running UI.
---

# Test E2E (Playwright NUnit)

Project: `tests/Forum.Tests.E2E`. **32 test** bao phủ auth, tạo/sửa/xóa chủ đề, bình luận lồng nhau, vote, tìm kiếm/lọc, phân trang, bookmark, thông báo/@mention, hover card, dark mode, phân quyền, kiểm duyệt, SEO (title/canonical/JSON-LD/slug-301), sitemap/robots, chat (text, upload ảnh + lightbox, giới hạn 1MB user thường / staff không giới hạn), shoutbox.

## Cách chạy
1. **Dừng app dev nếu đang chạy** (khóa DLL → build lỗi):
   ```bash
   taskkill //F //IM dotnet.exe 2>/dev/null
   ```
2. **Build** (cần để fixture chạy DLL đã build):
   ```bash
   dotnet build Forum.sln -v q --nologo
   ```
3. **Cài trình duyệt Chromium** (chỉ lần đầu):
   ```bash
   pwsh tests/Forum.Tests.E2E/bin/Debug/net8.0/playwright.ps1 install chromium
   # Windows không có pwsh: powershell -File <đường-dẫn>/playwright.ps1 install chromium
   ```
4. **Chạy toàn bộ:**
   ```bash
   dotnet test tests/Forum.Tests.E2E/Forum.Tests.E2E.csproj --no-build --logger "console;verbosity=minimal"
   ```
   Hoặc một lệnh: `./scripts/test.ps1` / `./scripts/test.sh`.

## Cơ chế
- `ServerFixture` ([SetUpFixture]) tự khởi chạy DLL trên **http://127.0.0.1:5099** với DB riêng `forum-test.db` (xóa + seed lại mỗi lần ⇒ idempotent), chờ sẵn sàng, tắt khi xong. Test chạy **tuần tự** (NonParallelizable).
- Có thể chạy song song với app dev (cổng 5080) vì khác cổng/DB — nhưng `dotnet test` không rebuild app dev, còn `dotnet build` thì sẽ lỗi nếu app dev đang chạy.

## Chụp ảnh xác minh trực quan
Test `Diagnostics.Capture_Screens` là **`[Explicit]`** (không chạy trong suite). Chạy riêng để chụp các màn hình vào `shots/`:
```bash
dotnet test tests/Forum.Tests.E2E/Forum.Tests.E2E.csproj --no-build --filter "Name=Capture_Screens"
```
Sau đó dùng tool Read trên `shots/*.png` để xem. Thêm bước chụp mới bằng cách sửa `Diagnostics.cs` (dùng `Page.Locator(...).ScreenshotAsync` để crop một phần tử). Upload file trong test: `SetInputFilesAsync(new FilePayload{...})` hoạt động cả với input ẩn — chờ phần tử **hiển thị** (vd `.cw-text`), không chờ input file ẩn.

## Khi thêm/sửa luồng
Thêm test vào file phù hợp (`AuthTests`, `TopicTests`, `CommentVoteTests`, `ChatTests`, `ModerationAuthTests`, `SeoTests`, `SearchAndPagingTests`, `InteractionTests`). Selector bám theo markup hiện có; tránh dựa vào chủ đề ghim "Nội quy" (bị khóa) cho luồng cần bình luận — dùng `OpenCategoryTopicAsync("cua-go")`. **Chỉ báo xong khi suite xanh hết.**
