using System.Text.Json;
using Bogus;
using Forum.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Forum.Web.Data.Seed;

/// <summary>
/// Đổ dữ liệu mẫu ngành xây dựng vào DB khi khởi chạy lần đầu (idempotent).
/// Nội dung tiếng Việt lấy từ các file JSON dưới Data/Seed/content; số liệu &amp; ngày
/// sinh bằng Bogus (seed cố định -> tái lập được).
/// </summary>
public class SeedService
{
    public const string DemoPassword = "Test@123";

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<ApplicationRole> _roles;
    private readonly ISlugService _slug;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<SeedService> _log;

    private readonly Faker _f;
    private readonly Dictionary<string, Tag> _tags = new();

    private ApplicationUser _admin = null!, _mod = null!, _demo = null!;
    private List<ApplicationUser> _pool = new();

    public SeedService(ApplicationDbContext db, UserManager<ApplicationUser> users,
        RoleManager<ApplicationRole> roles, ISlugService slug, IWebHostEnvironment env, ILogger<SeedService> log)
    {
        _db = db; _users = users; _roles = roles; _slug = slug; _env = env; _log = log;
        Randomizer.Seed = new Random(73);
        _f = new Faker();
    }

    public async Task SeedAsync()
    {
        await _db.Database.MigrateAsync();
        await EnsureCmsPagesAsync();   // chạy luôn (kể cả DB cũ) để đảm bảo trang Nội quy tồn tại
        if (await _db.Categories.AnyAsync())
        {
            _log.LogInformation("Dữ liệu đã tồn tại — bỏ qua seed.");
            return;
        }
        _log.LogInformation("Bắt đầu đổ dữ liệu mẫu…");

        await SeedRolesAsync();
        await SeedUsersAsync();
        await SeedBadgesAsync();
        var cats = await SeedCategoriesAsync();
        await SeedContentAsync(cats);
        await SeedReputationAndBadgesAsync();
        await SeedInteractionsAsync();
        await SeedNotificationsAsync();
        await SeedReportsAndModerationAsync();
        await SeedChatAsync();
        await SeedShoutsAsync();

        _log.LogInformation("Seed hoàn tất: {Users} users, {Topics} topics, {Comments} comments.",
            await _db.Users.CountAsync(), await _db.Topics.CountAsync(), await _db.Comments.CountAsync());
    }

    // ---- Roles ----
    private async Task SeedRolesAsync()
    {
        foreach (var r in Roles.All)
            if (!await _roles.RoleExistsAsync(r))
                await _roles.CreateAsync(new ApplicationRole { Name = r });
    }

    // ---- Users ----
    private async Task SeedUsersAsync()
    {
        DateTime Joined(int maxDaysAgo) =>
            _f.Date.BetweenOffset(DateTimeOffset.UtcNow.AddDays(-maxDaysAgo), DateTimeOffset.UtcNow.AddDays(-2)).UtcDateTime;

        async Task<ApplicationUser> Create(string username, string display, string role, UserTrade trade, string? bio, string? loc, int maxDaysAgo)
        {
            var u = new ApplicationUser
            {
                UserName = username,
                Email = $"{username}@cuaforum.vn",
                EmailConfirmed = true,
                DisplayName = display,
                Trade = trade,
                Bio = bio,
                Location = loc,
                CreatedAt = Joined(maxDaysAgo),
                LastActiveAt = _f.Date.RecentOffset(20).UtcDateTime
            };
            var res = await _users.CreateAsync(u, DemoPassword);
            if (!res.Succeeded)
                throw new InvalidOperationException("Tạo user thất bại: " + string.Join("; ", res.Errors.Select(e => e.Description)));
            await _users.AddToRoleAsync(u, role);
            return u;
        }

        // Tài khoản demo cố định (ghi trong README).
        _admin = await Create("admin", "Quản trị viên", Roles.Admin, UserTrade.KySuVatLieu,
            "Quản trị diễn đàn Xây dựng Việt. Liên hệ khi cần hỗ trợ kỹ thuật/bài viết.", "Hà Nội", 700);
        _mod = await Create("mod", "Điều hành viên", Roles.Moderator, UserTrade.ThoLapDat,
            "Điều hành nội dung, hỗ trợ thành viên mới.", "TP.HCM", 600);
        _demo = await Create("demo", "Bạn Demo", Roles.Member, UserTrade.ChuNha,
            "Tài khoản dùng thử — đang tìm cửa cho nhà mới.", "Đà Nẵng", 120);

        // 40 nhân vật từ file JSON.
        var file = ReadJson<SeedUserFile>("_users.json");
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "admin", "mod", "demo" };
        foreach (var su in file?.Users ?? new())
        {
            var uname = su.Username.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(uname) || !usedNames.Add(uname)) continue;
            var trade = Enum.TryParse<UserTrade>(su.Trade, out var t) ? t : UserTrade.Khac;
            // ~1 trên 12 nhân vật được nâng quyền Moderator để đa dạng.
            var role = _f.Random.Int(0, 11) == 0 ? Roles.Moderator : Roles.Member;
            var u = await Create(uname, su.DisplayName, role, trade, su.Bio, su.Location, 730);
            _pool.Add(u);
        }

        // Pool tác giả gồm tất cả (kể cả admin/mod/demo) để nội dung đa dạng.
        _pool.Add(_admin); _pool.Add(_mod); _pool.Add(_demo);
        _log.LogInformation("Đã tạo {Count} người dùng.", _pool.Count);
    }

    // ---- Badges ----
    private async Task SeedBadgesAsync()
    {
        var badges = new[]
        {
            new Badge { Slug = BadgeSlugs.NewMember, Name = "Thành viên mới", Description = "Chào mừng gia nhập cộng đồng Xây dựng Việt.", IconName = "user", ColorHex = "#a86a32", Tier = BadgeTier.Bronze },
            new Badge { Slug = BadgeSlugs.FirstTopic, Name = "Bài đầu tiên", Description = "Đăng chủ đề thảo luận đầu tiên.", IconName = "edit-3", ColorHex = "#a86a32", Tier = BadgeTier.Bronze },
            new Badge { Slug = BadgeSlugs.Contributor, Name = "Người đóng góp", Description = "Viết từ 10 bình luận hữu ích.", IconName = "message-square", ColorHex = "#707880", Tier = BadgeTier.Silver },
            new Badge { Slug = BadgeSlugs.Popular, Name = "Được yêu thích", Description = "Có chủ đề đạt 10+ điểm.", IconName = "heart", ColorHex = "#707880", Tier = BadgeTier.Silver },
            new Badge { Slug = BadgeSlugs.Expert, Name = "Chuyên gia xây dựng", Description = "Đạt 500+ điểm uy tín.", IconName = "award", ColorHex = "#b7860b", Tier = BadgeTier.Gold },
            new Badge { Slug = BadgeSlugs.Veteran, Name = "Kỳ cựu", Description = "Gắn bó trên 6 tháng.", IconName = "shield", ColorHex = "#b7860b", Tier = BadgeTier.Gold },
        };
        _db.Badges.AddRange(badges);
        await _db.SaveChangesAsync();
    }

    // ---- Categories ----
    private async Task<Dictionary<string, Category>> SeedCategoriesAsync()
    {
        var defs = new (string slug, string name, string desc, string icon, string color)[]
        {
            ("ket-cau-thi-cong", "Kết cấu & Thi công", "Móng, cột, dầm, sàn, bê tông cốt thép, biện pháp thi công.", "hammer", "#e8590c"),
            ("vat-lieu-xay-dung", "Vật liệu xây dựng", "Xi măng, gạch, cát đá, thép, phụ gia — so sánh & báo giá.", "layers", "#b06a2c"),
            ("dien-nuoc", "Điện & Nước (M&E)", "Đi dây điện, cấp thoát nước, thiết bị vệ sinh, an toàn điện.", "zap", "#f08c00"),
            ("chong-tham-son", "Chống thấm & Sơn", "Chống thấm mái/nhà vệ sinh/tường, sơn nội ngoại thất.", "droplets", "#1c7ed6"),
            ("cua-nhom-kinh", "Cửa, nhôm kính & vách", "Cửa gỗ, nhôm Xingfa, kính cường lực, vách ngăn, cửa cuốn.", "door-open", "#2f9e44"),
            ("noi-that-hoan-thien", "Nội thất & Hoàn thiện", "Trần thạch cao, sàn gỗ/gạch, tủ bếp, nội thất, hoàn thiện.", "home", "#9c36b5"),
            ("phong-thuy-nha-o", "Phong thủy nhà ở", "Hướng nhà, bố trí, kích thước Lỗ Ban, ngũ hành, tuổi làm nhà.", "compass", "#c2255c"),
            ("nha-thau-bao-gia", "Nhà thầu, tư vấn & Báo giá", "Chọn nhà thầu, đơn giá xây thô/trọn gói, hợp đồng, dự toán.", "bar-chart", "#0c8599"),
        };
        var map = new Dictionary<string, Category>();
        var order = 1;
        foreach (var d in defs)
        {
            var c = new Category { Slug = d.slug, Name = d.name, Description = d.desc, IconName = d.icon, ColorHex = d.color, DisplayOrder = order++, CreatedAt = DateTime.UtcNow.AddDays(-730) };
            _db.Categories.Add(c);
            map[d.slug] = c;
        }
        await _db.SaveChangesAsync();
        return map;
    }

    private Tag GetOrCreateTag(string name)
    {
        var slug = _slug.Generate(name, 50);
        if (_tags.TryGetValue(slug, out var existing)) return existing;
        var tag = new Tag { Name = name.Trim(), Slug = slug, UseCount = 0, CreatedAt = DateTime.UtcNow.AddDays(-700) };
        _db.Tags.Add(tag);
        _tags[slug] = tag;
        return tag;
    }

    // ---- Content (topics, comments, polls, tags) ----
    private async Task SeedContentAsync(Dictionary<string, Category> cats)
    {
        // Chủ đề ghim cố định (Nội quy) do admin đăng, dùng cho demo/khóa.
        var rules = new Topic
        {
            Title = "📌 Nội quy diễn đàn & hướng dẫn đăng bài",
            Slug = _slug.Generate("Nội quy diễn đàn & hướng dẫn đăng bài", 120),
            Body = "Chào mừng tới **Diễn đàn Xây dựng Việt**!\n\n- Đăng đúng danh mục, tiêu đề rõ ràng.\n- Không spam, không quảng cáo trá hình.\n- Tôn trọng thành viên khác, chia sẻ kinh nghiệm thi công thực tế.\n\n> Vi phạm nhiều lần sẽ bị khóa tài khoản.",
            CategoryId = cats["nha-thau-bao-gia"].Id,
            Author = _admin, AuthorId = _admin.Id,
            IsPinned = true, IsLocked = true, IsFeatured = false, IsQuestion = false,
            CreatedAt = DateTime.UtcNow.AddDays(-200), LastActivityAt = DateTime.UtcNow.AddDays(-10),
            ViewCount = _f.Random.Int(2000, 6000),
            UpvoteCount = _f.Random.Int(80, 160), DownvoteCount = _f.Random.Int(0, 5)
        };
        rules.Score = rules.UpvoteCount - rules.DownvoteCount;
        rules.HotScore = Ranking.HotScore(rules.Score, rules.CreatedAt);
        rules.TopicTags.Add(new TopicTag { Tag = GetOrCreateTag("nội-quy") });
        _db.Topics.Add(rules);
        await _db.SaveChangesAsync();

        foreach (var (slug, cat) in cats)
        {
            var file = ReadJson<SeedCategoryFile>($"{slug}.json");
            if (file is null) { _log.LogWarning("Thiếu file nội dung {Slug}.json", slug); continue; }

            foreach (var st in file.Topics)
            {
                var author = _f.PickRandom(_pool);
                var created = _f.Date.BetweenOffset(DateTimeOffset.UtcNow.AddDays(-120), DateTimeOffset.UtcNow.AddDays(-1)).UtcDateTime;

                var hype = st.Featured || st.Pinned;
                var up = hype ? _f.Random.Int(45, 280) : _f.Random.Int(0, 60);
                var down = _f.Random.Int(0, Math.Max(1, up / 8));

                var topic = new Topic
                {
                    Title = st.Title.Trim(),
                    Slug = _slug.Generate(st.Title, 120),
                    Body = st.Body,
                    CategoryId = cat.Id,
                    Author = author, AuthorId = author.Id,
                    IsQuestion = st.IsQuestion,
                    IsFeatured = st.Featured,
                    IsPinned = st.Pinned,
                    CreatedAt = created,
                    LastActivityAt = created,
                    ViewCount = (hype ? _f.Random.Int(800, 5000) : _f.Random.Int(40, 1500)),
                    UpvoteCount = up,
                    DownvoteCount = down,
                    Score = up - down
                };
                topic.HotScore = Ranking.HotScore(topic.Score, created);

                foreach (var tagName in st.Tags.Take(6))
                {
                    var tag = GetOrCreateTag(tagName);
                    topic.TopicTags.Add(new TopicTag { Tag = tag });
                    tag.UseCount++;
                }

                _db.Topics.Add(topic);
                await _db.SaveChangesAsync(); // lấy topic.Id

                // Poll
                if (st.Poll is { Options.Count: >= 2 })
                {
                    var poll = new Poll { TopicId = topic.Id, Question = st.Poll.Question, AllowMultiple = false, CreatedAt = created };
                    var oi = 0;
                    foreach (var opt in st.Poll.Options.Take(6))
                        poll.Options.Add(new PollOption { Text = opt, DisplayOrder = oi++, VoteCount = _f.Random.Int(3, 120) });
                    _db.Polls.Add(poll);
                }

                // Comments (cây lồng nhau + materialized path)
                await SeedCommentTreeAsync(topic, st.Comments);
                await _db.SaveChangesAsync();
            }
        }

        await _db.SaveChangesAsync();
        _log.LogInformation("Đã tạo nội dung. Tổng tag: {Tags}", _tags.Count + 1);
    }

    private async Task SeedCommentTreeAsync(Topic topic, List<SeedComment> seedComments)
    {
        if (seedComments.Count == 0) { topic.LastActivityAt = topic.CreatedAt; return; }

        var created = new List<Comment>();
        var clock = topic.CreatedAt;

        void Build(List<SeedComment> nodes, Comment? parent, int depth)
        {
            foreach (var sc in nodes)
            {
                if (string.IsNullOrWhiteSpace(sc.Body)) continue;
                clock = clock.AddMinutes(_f.Random.Int(20, 2880));
                if (clock > DateTime.UtcNow) clock = DateTime.UtcNow.AddMinutes(-_f.Random.Int(1, 600));

                var author = _f.PickRandom(_pool);
                var up = _f.Random.Int(0, 14);
                var down = _f.Random.Int(0, 3);
                var c = new Comment
                {
                    TopicId = topic.Id,
                    Author = author, AuthorId = author.Id,
                    ParentComment = parent,
                    Body = sc.Body,
                    Depth = depth,
                    CreatedAt = clock,
                    UpvoteCount = up, DownvoteCount = down, Score = up - down
                };
                _db.Comments.Add(c);
                created.Add(c);
                if (sc.Replies.Count > 0) Build(sc.Replies, c, depth + 1);
            }
        }
        Build(seedComments, null, 0);
        await _db.SaveChangesAsync(); // gán Id

        // Materialized path: xử lý theo độ sâu tăng dần (cha trước con).
        foreach (var c in created.OrderBy(x => x.Depth))
            c.Path = c.ParentComment is null ? c.Id.ToString("D7") : $"{c.ParentComment.Path}/{c.Id:D7}";

        topic.CommentCount = created.Count;
        topic.LastActivityAt = created.Max(c => c.CreatedAt);
    }

    // ---- Reputation & badges ----
    private async Task SeedReputationAndBadgesAsync()
    {
        var allUsers = await _db.Users.ToListAsync();
        var badges = await _db.Badges.ToDictionaryAsync(b => b.Slug);

        foreach (var u in allUsers)
        {
            var topics = await _db.Topics.Where(t => t.AuthorId == u.Id && !t.IsDeleted)
                .Select(t => new { t.UpvoteCount, t.DownvoteCount, t.Score }).ToListAsync();
            var comments = await _db.Comments.Where(c => c.AuthorId == u.Id && !c.IsDeleted)
                .Select(c => new { c.UpvoteCount, c.DownvoteCount }).ToListAsync();

            var rep = 10; // base
            foreach (var t in topics) rep += t.UpvoteCount * ReputationPoints.TopicUpvote + t.DownvoteCount * ReputationPoints.TopicDownvote;
            foreach (var c in comments) rep += c.UpvoteCount * ReputationPoints.CommentUpvote + c.DownvoteCount * ReputationPoints.CommentDownvote;
            u.Reputation = Math.Max(0, rep);

            void Award(string slug, bool cond)
            {
                if (!cond || !badges.TryGetValue(slug, out var b)) return;
                _db.UserBadges.Add(new UserBadge { UserId = u.Id, BadgeId = b.Id, AwardedAt = u.CreatedAt.AddDays(_f.Random.Int(1, 60)) });
            }
            Award(BadgeSlugs.NewMember, true);
            Award(BadgeSlugs.FirstTopic, topics.Count >= 1);
            Award(BadgeSlugs.Contributor, comments.Count >= 10);
            Award(BadgeSlugs.Popular, topics.Any(t => t.Score >= 10));
            Award(BadgeSlugs.Expert, u.Reputation >= 500);
            Award(BadgeSlugs.Veteran, (DateTime.UtcNow - u.CreatedAt).TotalDays >= 180);
        }
        await _db.SaveChangesAsync();
    }

    // ---- Interactions: bookmarks, subscriptions, demo votes ----
    private async Task SeedInteractionsAsync()
    {
        var topicIds = await _db.Topics.Where(t => !t.IsDeleted).OrderByDescending(t => t.Score).Select(t => t.Id).Take(40).ToListAsync();

        // Demo: bookmark + theo dõi + vote thực để hiển thị trạng thái.
        foreach (var tid in topicIds.Take(5))
            _db.Bookmarks.Add(new Bookmark { UserId = _demo.Id, TopicId = tid, CreatedAt = DateTime.UtcNow.AddDays(-_f.Random.Int(1, 30)) });
        foreach (var tid in topicIds.Skip(2).Take(4))
            _db.TopicSubscriptions.Add(new TopicSubscription { UserId = _demo.Id, TopicId = tid, CreatedAt = DateTime.UtcNow.AddDays(-_f.Random.Int(1, 30)) });

        foreach (var tid in topicIds.Skip(1).Take(6))
        {
            _db.TopicVotes.Add(new TopicVote { UserId = _demo.Id, TopicId = tid, Value = 1, CreatedAt = DateTime.UtcNow.AddDays(-_f.Random.Int(1, 20)) });
            var t = await _db.Topics.FindAsync(tid);
            if (t != null) { t.UpvoteCount++; t.Score = t.UpvoteCount - t.DownvoteCount; }
        }

        // Demo theo dõi vài thành viên (cho hover card).
        foreach (var u in _pool.Where(u => u.Id != _demo.Id).Take(5))
            _db.UserFollows.Add(new UserFollow { FollowerId = _demo.Id, FolloweeId = u.Id, CreatedAt = DateTime.UtcNow.AddDays(-_f.Random.Int(1, 60)) });

        // Một ít bookmark ngẫu nhiên cho thành viên khác.
        foreach (var u in _pool.Take(15))
            foreach (var tid in _f.PickRandom(topicIds, Math.Min(3, topicIds.Count)))
                if (!await _db.Bookmarks.AnyAsync(b => b.UserId == u.Id && b.TopicId == tid))
                    _db.Bookmarks.Add(new Bookmark { UserId = u.Id, TopicId = tid, CreatedAt = DateTime.UtcNow.AddDays(-_f.Random.Int(1, 90)) });

        await _db.SaveChangesAsync();
    }

    // ---- Notifications (cho tài khoản demo) ----
    private async Task SeedNotificationsAsync()
    {
        var demoTopic = await _db.Topics.FirstOrDefaultAsync(t => t.AuthorId == _demo.Id && !t.IsDeleted);
        var someUser = _pool.First(u => u.Id != _demo.Id);

        if (demoTopic != null)
        {
            _db.Notifications.Add(new Notification
            {
                RecipientId = _demo.Id, ActorId = someUser.Id, Type = NotificationType.Reply,
                TopicId = demoTopic.Id, Url = $"/chu-de/{demoTopic.Id}/{demoTopic.Slug}",
                IsRead = false, CreatedAt = DateTime.UtcNow.AddHours(-3)
            });
            _db.Notifications.Add(new Notification
            {
                RecipientId = _demo.Id, ActorId = _pool[1].Id, Type = NotificationType.Mention,
                TopicId = demoTopic.Id, Url = $"/chu-de/{demoTopic.Id}/{demoTopic.Slug}",
                IsRead = false, CreatedAt = DateTime.UtcNow.AddHours(-8)
            });
        }
        _db.Notifications.Add(new Notification
        {
            RecipientId = _demo.Id, ActorId = _pool[2].Id, Type = NotificationType.Follow,
            Url = $"/thanh-vien/{_pool[2].UserName}", IsRead = false, CreatedAt = DateTime.UtcNow.AddDays(-1)
        });
        _db.Notifications.Add(new Notification
        {
            RecipientId = _demo.Id, Type = NotificationType.Badge,
            Message = "Bạn vừa nhận huy hiệu \"Thành viên mới\"!",
            Url = $"/thanh-vien/{_demo.UserName}", IsRead = true, CreatedAt = DateTime.UtcNow.AddDays(-2)
        });
        await _db.SaveChangesAsync();
    }

    // ---- Reports & moderation log ----
    private async Task SeedReportsAndModerationAsync()
    {
        var topics = await _db.Topics.Where(t => !t.IsDeleted).OrderBy(t => t.Id).Take(30).ToListAsync();
        var comments = await _db.Comments.Where(c => !c.IsDeleted).OrderBy(c => c.Id).Take(50).ToListAsync();

        if (topics.Count > 3)
            _db.Reports.Add(new Report
            {
                ReporterId = _demo.Id, TargetType = ContentTargetType.Topic, TopicId = topics[3].Id,
                Reason = "Spam/Quảng cáo", Details = "Nghi ngờ quảng cáo trá hình, đăng nhiều link.",
                Status = ReportStatus.Pending, CreatedAt = DateTime.UtcNow.AddHours(-20)
            });
        if (comments.Count > 5)
            _db.Reports.Add(new Report
            {
                ReporterId = _pool[4].Id, TargetType = ContentTargetType.Comment, CommentId = comments[5].Id,
                Reason = "Nội dung không phù hợp", Details = "Dùng từ ngữ chưa lịch sự.",
                Status = ReportStatus.Pending, CreatedAt = DateTime.UtcNow.AddHours(-30)
            });

        var rules = await _db.Topics.FirstOrDefaultAsync(t => t.IsPinned && t.IsLocked);
        if (rules != null)
        {
            _db.ModerationLogs.Add(new ModerationLog { ModeratorId = _admin.Id, Action = ModerationAction.Pin, TargetType = ContentTargetType.Topic, TargetId = rules.Id, TargetTitle = rules.Title, CreatedAt = DateTime.UtcNow.AddDays(-200) });
            _db.ModerationLogs.Add(new ModerationLog { ModeratorId = _admin.Id, Action = ModerationAction.Lock, TargetType = ContentTargetType.Topic, TargetId = rules.Id, TargetTitle = rules.Title, Detail = "Khóa để giữ nội quy cố định.", CreatedAt = DateTime.UtcNow.AddDays(-200) });
        }
        await _db.SaveChangesAsync();
    }

    // ---- Chat ----
    private async Task SeedChatAsync()
    {
        var partners = _pool.Where(u => u.Id != _demo.Id).Take(7).ToList();
        foreach (var (partner, i) in partners.Select((p, i) => (p, i)))
        {
            var conv = new Conversation { CreatedAt = DateTime.UtcNow.AddDays(-(i + 1)), LastMessageAt = DateTime.UtcNow.AddMinutes(-_f.Random.Int(5, 600)) };
            conv.Participants.Add(new ConversationParticipant { User = _demo, LastReadAt = DateTime.UtcNow.AddMinutes(-30) });
            conv.Participants.Add(new ConversationParticipant { User = partner, LastReadAt = DateTime.UtcNow.AddHours(-2) });
            _db.Conversations.Add(conv);
            await _db.SaveChangesAsync();

            var lines = new[]
            {
                (_demo, "Chào bạn, mình thấy bạn rành về cửa nhôm, cho hỏi chút được không?"),
                (partner, "Chào bạn, được chứ. Bạn cần tư vấn loại nào?"),
                (_demo, "Mình định làm cửa ban công, phân vân Xingfa với PMA."),
                (partner, "Xingfa hệ 55 là ổn cho ban công, cách âm tốt. Bạn ở khu nào để mình giới thiệu đại lý uy tín."),
            };
            var t = conv.CreatedAt;
            foreach (var (sender, body) in lines)
            {
                t = t.AddMinutes(_f.Random.Int(2, 90));
                _db.ChatMessages.Add(new ChatMessage { ConversationId = conv.Id, SenderId = sender.Id, Body = body, CreatedAt = t });
            }
            conv.LastMessageAt = t;
            await _db.SaveChangesAsync();
        }
    }

    // ---- Chat chung (shoutbox) ----
    private async Task SeedShoutsAsync()
    {
        var lines = new[]
        {
            "Chào cả nhà, ai có kinh nghiệm cửa nhôm Xingfa cho mình xin tư vấn với!",
            "Mọi người ơi báo giá cửa cuốn Austdoor giờ khoảng bao nhiêu /m² vậy?",
            "Vừa lắp xong bộ cửa gỗ HDF veneer sồi, đẹp lắm 😍",
            "Cho hỏi cửa nhựa composite có bền với nhà vệ sinh không ạ?",
            "Ai ở Hà Nội cần thợ lắp cửa kính cường lực thì inbox mình nhé 🔧",
            "Cửa chống cháy EI60 với EI90 khác nhau nhiều không mọi người?",
            "Box phong thủy cửa hay phết, vừa đọc xong bài hướng cửa mệnh Kim 👍",
            "Ai xài khóa vân tay loại nào tốt mà giá ổn chỉ mình với",
            "Giá nhôm dạo này tăng hay giảm vậy các bác?",
            "Cảm ơn admin đã tổng hợp báo giá 2026, hữu ích quá!",
            "Có ai so sánh uPVC lõi thép với nhôm cầu cách nhiệt chưa nhỉ?",
            "Diễn đàn mình đông vui ghê 🎉",
        };
        var t = DateTime.UtcNow.AddHours(-6);
        foreach (var line in lines)
        {
            t = t.AddMinutes(_f.Random.Int(5, 40));
            if (t > DateTime.UtcNow) t = DateTime.UtcNow.AddMinutes(-_f.Random.Int(1, 30));
            var sender = _f.PickRandom(_pool);
            _db.ShoutMessages.Add(new ShoutMessage { SenderId = sender.Id, Body = line, CreatedAt = t });
        }
        await _db.SaveChangesAsync();
    }

    // ---- helpers ----
    private T? ReadJson<T>(string fileName) where T : class
    {
        var path = Path.Combine(_env.ContentRootPath, "Data", "Seed", "content", fileName);
        if (!File.Exists(path))
            path = Path.Combine(AppContext.BaseDirectory, "Data", "Seed", "content", fileName);
        if (!File.Exists(path)) { _log.LogWarning("Không tìm thấy file seed: {File}", fileName); return null; }
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    /// <summary>Đảm bảo các trang CMS mặc định tồn tại (idempotent, chạy cả trên DB cũ).</summary>
    private async Task EnsureCmsPagesAsync()
    {
        if (!await _db.CmsPages.AnyAsync(p => p.Slug == "noi-quy"))
        {
            _db.CmsPages.Add(new CmsPage
            {
                Slug = "noi-quy",
                Title = "Nội quy diễn đàn",
                IsPublished = true,
                UpdatedAt = DateTime.UtcNow,
                Body =
@"Chào mừng bạn đến với **Diễn đàn Xây dựng Việt** — cộng đồng thảo luận về cửa & vật liệu cửa.

## Quy tắc chung
1. **Đăng đúng danh mục**, đặt tiêu đề rõ ràng, dễ tìm.
2. **Không spam, không quảng cáo trá hình**, không rải link.
3. **Tôn trọng** các thành viên khác; không công kích cá nhân.
4. Nội dung phải **đúng chủ đề về cửa** (gỗ, nhôm kính, cuốn, uPVC, chống cháy, phụ kiện, phong thủy…).

## Xử lý vi phạm
Bài viết vi phạm có thể bị **ẩn/xóa**; tài khoản vi phạm nhiều lần có thể bị **cảnh cáo hoặc khóa**.

Cảm ơn bạn đã góp phần xây dựng cộng đồng văn minh và hữu ích!"
            });
            await _db.SaveChangesAsync();
            _log.LogInformation("Đã tạo trang CMS mặc định: noi-quy");
        }
    }
}
