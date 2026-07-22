# Lược đồ cơ sở dữ liệu

EF Core (Code-First) + SQLite. Khóa chính kiểu `int` (IDENTITY). Thiết kế trung lập provider.
Định nghĩa entity: `src/Forum.Web/Models/Entities/`. Cấu hình: `src/Forum.Web/Data/ApplicationDbContext.cs`.

## Tổng quan quan hệ

```
ApplicationUser 1───* Topic *───1 Category (ParentCategory tự tham chiếu, tùy chọn)
Topic 1───* Comment (Comment.ParentCommentId tự tham chiếu → cây bình luận; Path + Depth = materialized path)
Topic *───* Tag  (qua TopicTag)
Topic 1───0..1 Poll 1───* PollOption 1───* PollVote
Topic 1───* TopicVote ;  Comment 1───* CommentVote        (Value = +1 / -1, unique theo (User, Target))
Topic 1───* Bookmark ;  Topic 1───* TopicSubscription      (composite key (UserId, TopicId))
ApplicationUser *───* ApplicationUser (UserFollow: Follower/Followee)
Notification *──1 Recipient ; *──0..1 Actor / Topic / Comment
Report, ModerationLog → kiểm duyệt
Badge *───* ApplicationUser (qua UserBadge) ; UserActivity, Draft
Conversation 1───* ConversationParticipant ; Conversation 1───* ChatMessage
```

## Bảng

### Người dùng & danh tiếng
| Bảng | Cột chính | Ghi chú |
|---|---|---|
| `AspNetUsers` (ApplicationUser) | Id, UserName, Email, **DisplayName, Bio, AvatarUrl, Location, Trade, Reputation, CreatedAt, LastActiveAt** | Mở rộng IdentityUser&lt;int&gt;. `Trade` = vai trò ngành (enum). |
| `AspNetRoles` (ApplicationRole) | Id, Name, Description | Admin / Moderator / Member |
| `UserFollows` | **(FollowerId, FolloweeId)**, CreatedAt | Theo dõi người dùng (2 FK tới user). |
| `Badges` | Id, Name, **Slug**(unique), Description, IconName, ColorHex, Tier | Huy hiệu (Bronze/Silver/Gold). |
| `UserBadges` | **(UserId, BadgeId)**, AwardedAt | |
| `UserActivities` | Id, UserId, Type, TopicId?, CommentId?, CreatedAt | Feed/lịch sử hoạt động. Index (UserId, CreatedAt). |
| `Drafts` | Id, UserId, CategoryId?, Title, Body, TagsCsv, UpdatedAt | Nháp tự lưu khi soạn bài. |

### Nội dung
| Bảng | Cột chính | Ghi chú |
|---|---|---|
| `Categories` | Id, Name, **Slug**(unique), Description, IconName, ColorHex, DisplayOrder, ParentCategoryId? | Danh mục/box. |
| `Topics` | Id, Title, **Slug**(index), Body(markdown), CategoryId, AuthorId, CreatedAt, UpdatedAt?, LastActivityAt, ViewCount, IsPinned, IsLocked, IsFeatured, IsDeleted, IsQuestion, **Score, UpvoteCount, DownvoteCount, CommentCount, HotScore** | Số liệu denormalized để sắp xếp nhanh. Index: (CategoryId, LastActivityAt), CreatedAt, HotScore, Score. |
| `Comments` | Id, TopicId, AuthorId, **ParentCommentId?**, Body, **Path**(materialized), Depth, CreatedAt, UpdatedAt?, IsDeleted, Score, UpvoteCount, DownvoteCount | Cây bình luận sắp xếp theo `Path`. Index (TopicId, Path). |
| `Tags` | Id, Name, **Slug**(unique), Description, UseCount, CreatedAt | |
| `TopicTags` | **(TopicId, TagId)** | Bảng nối nhiều-nhiều. |

### Tương tác
| Bảng | Cột chính | Ghi chú |
|---|---|---|
| `TopicVotes` | Id, TopicId, UserId, Value(±1), CreatedAt | **Unique (UserId, TopicId)**. |
| `CommentVotes` | Id, CommentId, UserId, Value(±1), CreatedAt | **Unique (UserId, CommentId)**. |
| `Bookmarks` | **(UserId, TopicId)**, CreatedAt | Lưu chủ đề. |
| `TopicSubscriptions` | **(UserId, TopicId)**, CreatedAt | Theo dõi để nhận thông báo. |
| `Notifications` | Id, RecipientId, ActorId?, Type, TopicId?, CommentId?, Message?, Url?, IsRead, CreatedAt | Index (RecipientId, IsRead, CreatedAt). |

### Bình chọn & đính kèm
| Bảng | Cột chính |
|---|---|
| `Polls` | Id, TopicId(1-1), Question, AllowMultiple, ClosesAt?, CreatedAt |
| `PollOptions` | Id, PollId, Text, DisplayOrder, VoteCount |
| `PollVotes` | Id, PollId, PollOptionId, UserId, CreatedAt — index (PollId, UserId) |
| `Attachments` | Id, UploaderId, TopicId?/CommentId?, FileName, StoredPath, ContentType, SizeBytes, IsImage, AltText, CreatedAt |

### Kiểm duyệt
| Bảng | Cột chính |
|---|---|
| `Reports` | Id, ReporterId, TargetType, TopicId?/CommentId?, Reason, Details?, Status, ResolvedById?, ResolvedAt?, CreatedAt |
| `ModerationLogs` | Id, ModeratorId, Action, TargetType, TargetId, TargetTitle?, Detail?, CreatedAt |

### Chat
| Bảng | Cột chính |
|---|---|
| `Conversations` | Id, CreatedAt, LastMessageAt |
| `ConversationParticipants` | **(ConversationId, UserId)**, LastReadAt? |
| `ChatMessages` | Id, ConversationId, SenderId, Body, CreatedAt — index (ConversationId, CreatedAt) |

### Cấu hình site (quản trị)
| Bảng | Cột chính |
|---|---|
| `SiteSettings` | **Key** (PK, ≤80), Value (≤4000) — key-value chỉnh lúc chạy ở `/quan-tri/cau-hinh` (tên site, mô tả, từ cấm). Đọc/cache qua `ISiteSettingService` (singleton). |

**Kiểm duyệt theo danh mục:** `Category.RequireApproval` (bool) — bật thì chủ đề đăng vào danh mục đó có `Topic.IsApproved=false` (chờ duyệt, ẩn khỏi danh sách công khai, chỉ tác giả/staff xem được). Duyệt/từ chối ở `/kiem-duyet` (`ModerationService.ApproveTopicAsync`/`RejectTopicAsync`).

### Quản trị mở rộng
| Bảng | Cột chính |
|---|---|
| `Announcements` | Id, Message, Url?, IsActive, DisplayOrder, StartsAt?, EndsAt?, CreatedAt — thông báo chạy do admin soạn (`/quan-tri/thong-bao`); ticker ưu tiên bảng này, fallback chủ đề ghim/nổi bật. |
| `UserWarnings` | Id, UserId, ModeratorId, Reason, CreatedAt — lịch sử cảnh cáo/khóa (khóa tạm dùng Identity `LockoutEnd`). |
| `CmsPages` | Id, **Slug** (unique), Title, Body(Markdown), IsPublished, UpdatedAt — trang tĩnh (`/noi-quy`, `/trang/{slug}`), sửa ở `/quan-tri/trang`. |
| `CategoryModerators` | **(CategoryId, UserId)** — mod phụ trách danh mục; mod có phân công chỉ thấy hàng chờ duyệt của danh mục mình. |

**Feature flags / thương hiệu / auto-mod** lưu trong `SiteSettings` (key-value), sửa ở `/quan-tri/cau-hinh`: `feature.registration|posting|chat|polls`, `brand.accent|logo|favicon`, `automod.newUser|newUserDays|spam`.

## Quy ước thiết kế

- **Soft delete:** `Topic.IsDeleted` / `Comment.IsDeleted` (không xóa cứng) ⇒ giữ toàn vẹn cây bình luận và lịch sử.
- **DeleteBehavior.Restrict** cho mọi quan hệ (tránh lỗi multiple cascade paths trên SQL Server).
- **Materialized path** cho bình luận lồng nhau (portable, không dùng `HierarchyId` đặc thù SQL Server).
- **Denormalized counters** (Score, CommentCount, HotScore…) cập nhật trong tầng service khi vote/bình luận.
- Enum lưu dạng `int`. `DateTime` lưu UTC.
