# Tài liệu Endpoint

URL tiếng Việt, slug thân thiện SEO. Endpoint AJAX nhận/trả JSON (camelCase) và yêu cầu header chống giả mạo `RequestVerificationToken` (JS `Forum.api` tự gắn). `[Auth]` = cần đăng nhập; `[Staff]` = Admin/Moderator.

## Trang (GET, render HTML)

| Method | Đường dẫn | Mô tả |
|---|---|---|
| GET | `/` | Trang chủ: nổi bật + feed (sort: `?sap-xep=moi\|noi-bat\|xu-huong`, `?trang=N`) |
| GET | `/danh-muc` | Danh sách danh mục |
| GET | `/danh-muc/{slug}` | Chủ đề theo danh mục (`?sap-xep`, `?trang`) |
| GET | `/the` · `/the/{slug}` | Danh sách thẻ · chủ đề theo thẻ |
| GET | `/chu-de/{id}/{slug?}` | Chi tiết chủ đề (sai slug → **301**; `?binh-luan=top\|moi\|cu`) |
| GET | `/tao-chu-de` `[Auth]` · `/chu-de/{id}/sua` `[Auth]` | Form tạo / sửa chủ đề |
| GET | `/tim-kiem` | Kết quả tìm kiếm (`q`, `danh-muc`, `the`, `thoi-gian`, `sap-xep`, `trang`) |
| GET | `/thanh-vien/{username}` | Hồ sơ (`?tab=chu-de\|binh-luan\|huy-hieu`) |
| GET | `/thanh-vien-tich-cuc` | Bảng xếp hạng |
| GET | `/cai-dat` `[Auth]` · `/bang-tin` `[Auth]` · `/da-luu` `[Auth]` | Cài đặt hồ sơ · bảng tin · đã lưu |
| GET | `/thong-bao` `[Auth]` | Trang thông báo |
| GET | `/tin-nhan` · `/tin-nhan/{id}` · `/tin-nhan/voi/{username}` `[Auth]` | Chat |
| GET | `/kiem-duyet` `[Staff]` | Bảng kiểm duyệt (báo cáo + audit log) |
| GET | `/noi-quy` | Nội quy |
| GET | `/sitemap.xml` · `/robots.txt` | SEO |

## Xác thực (Account)

| Method | Đường dẫn | Body |
|---|---|---|
| GET/POST | `/dang-ky` | DisplayName, UserName, Email, Password, ConfirmPassword, Trade |
| GET/POST | `/dang-nhap` | UserNameOrEmail, Password, RememberMe |
| POST | `/dang-xuat` `[Auth]` | — |
| GET | `/tu-choi-truy-cap` | Trang 403 |

## Chủ đề (Topics)

| Method | Đường dẫn | Mô tả |
|---|---|---|
| POST | `/tao-chu-de` `[Auth]` | Tạo (Title, CategoryId, Body, Tags, IsQuestion, AddPoll…) |
| POST | `/chu-de/{id}/sua` `[Auth, owner/Staff]` | Sửa |
| POST | `/chu-de/{id}/xoa` `[Auth, owner/Staff]` | Xóa (soft) → `{ok}` |
| POST | `/chu-de/{id}/luu` `[Auth]` | Toggle bookmark → `{bookmarked}` |
| POST | `/chu-de/{id}/theo-doi` `[Auth]` | Toggle theo dõi → `{subscribed}` |
| POST | `/bao-cao` `[Auth]` | `{type, id, reason, details}` → `{ok}` |
| POST | `/nhap/luu` `[Auth]` · GET `/nhap` `[Auth]` | Lưu / lấy nháp |

## Bình luận, vote, poll

| Method | Đường dẫn | Body → Kết quả |
|---|---|---|
| POST | `/binh-luan/them` `[Auth]` | `{topicId, parentId?, body}` → `{html, count}` |
| POST | `/binh-luan/{id}/sua` `[Auth, owner]` | `{body}` → `{html, edited}` |
| POST | `/binh-luan/{id}/xoa` `[Auth, owner/Staff]` | → `{ok}` |
| POST | `/vote/topic` · `/vote/comment` `[Auth]` | `{id, value:±1}` → `{score, up, down, userVote}` |
| POST | `/binh-chon/vote` `[Auth]` | `{optionId}` → `{total, votedOptionId, options[]}` |
| POST | `/markdown/preview` `[Auth]` | `{markdown}` → `{html}` |

## Hồ sơ, tìm kiếm, thông báo, upload

| Method | Đường dẫn | Mô tả |
|---|---|---|
| GET | `/thanh-vien/{username}/the` | HTML hover card |
| POST | `/thanh-vien/{username}/theo-doi` `[Auth]` | Toggle follow → `{following, followers}` |
| POST | `/cai-dat` `[Auth]` | Cập nhật hồ sơ |
| GET | `/tim-kiem/goi-y?q=` | Autocomplete → `{topics[], tags[]}` |
| GET | `/thong-bao/dropdown` `[Auth]` | HTML danh sách thông báo |
| GET | `/thong-bao/dem` `[Auth]` | `{count}` |
| POST | `/thong-bao/{id}/doc` · `/thong-bao/doc-tat-ca` `[Auth]` | Đánh dấu đã đọc |
| POST | `/tai-len` `[Auth]` | multipart `file` → `{url, isImage, name}` |

## Kiểm duyệt (Moderation) `[Staff]`

| Method | Đường dẫn | Body |
|---|---|---|
| POST | `/kiem-duyet/ghim` · `/khoa` · `/noi-bat` | `{id, on}` → `{ok}` |
| POST | `/kiem-duyet/di-chuyen` | `{id, categoryId}` |
| POST | `/kiem-duyet/bao-cao/giai-quyet` | `{id, dismiss}` |
| POST | `/kiem-duyet/xoa` | `{type, id}` |

## SignalR Hubs

| Hub | Đường dẫn | Sự kiện |
|---|---|---|
| ForumHub | `/hubs/forum` | server → client: `notify {count}`, `newTopic {id,title,url,…}` |
| ChatHub | `/hubs/chat` `[Auth]` | client→server `SendMessage(conversationId, body)`; server→client `message {…}`, `presence {userId, online}` |
