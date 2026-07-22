---
name: add-migration
description: Add an EF Core migration after changing entities/DbContext in the Diễn đàn Cửa project (SQLite, provider-neutral). Use when adding/altering a table or column, or when a model change needs a schema update.
---

# Thêm EF Core migration

DbContext: `src/Forum.Web/Data/ApplicationDbContext.cs`. Entities: `src/Forum.Web/Models/Entities/`. Migrations: `src/Forum.Web/Migrations/`. Công cụ `dotnet-ef` đã cài local (`.config/dotnet-tools.json`).

## Quy trình
1. **Sửa entity** (+ cấu hình Fluent API trong `OnModelCreating` nếu cần: maxlength cho cột chuỗi có index, FK…). Giữ **trung lập provider**: chỉ kiểu CLR chuẩn, `DateTime` UTC, không HierarchyId; mọi FK để `DeleteBehavior.Restrict` (đã set tự động bằng vòng lặp cuối `OnModelCreating`).
2. **Dừng app dev** (đang chạy sẽ khóa DLL → tạo migration lỗi):
   ```bash
   taskkill //F //IM dotnet.exe 2>/dev/null
   ```
3. **Tạo migration:**
   ```bash
   dotnet dotnet-ef migrations add <TênMigration> \
     --project src/Forum.Web/Forum.Web.csproj --startup-project src/Forum.Web/Forum.Web.csproj
   ```
4. **Build lại** (để biên dịch migration mới) rồi chạy app — `Database.MigrateAsync()` trong `SeedService.SeedAsync` **tự áp dụng** migration lúc khởi động. DB hiện có được nâng cấp, dữ liệu giữ nguyên.
   - Nếu cần **dữ liệu seed mới** (seed bỏ qua khi `Categories` đã tồn tại): xóa `src/Forum.Web/forum.db*` để seed lại từ đầu.
   - DB test (`forum-test.db`) tự xóa + seed lại mỗi lần chạy E2E → luôn có schema mới.

## Lưu ý
- Đặt DbSet mới trong `ApplicationDbContext` và cấu hình entity trong `OnModelCreating`.
- Nếu thêm cột vào bảng đang dùng (vd `ChatMessage`), cột nullable hoặc có default để migration áp dụng êm trên DB có sẵn.
- Cập nhật `docs/DATABASE.md` khi thêm bảng/cột.
- Sau khi tạo migration, **build solution và chạy lại E2E** (skill `e2e-test`) để chắc không hồi quy.
