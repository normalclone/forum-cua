using Forum.Web.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Forum.Web.Data;

/// <summary>
/// DbContext chính. Kế thừa IdentityDbContext với khóa int.
/// Cấu hình giữ trung lập provider: chỉ dùng kiểu CLR chuẩn, không dùng tính năng
/// đặc thù SQLite. Mọi quan hệ đặt DeleteBehavior.Restrict để tránh lỗi
/// "multiple cascade paths" trên SQL Server; việc xóa dùng soft-delete trong service.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, int>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<TopicTag> TopicTags => Set<TopicTag>();
    public DbSet<TopicVote> TopicVotes => Set<TopicVote>();
    public DbSet<CommentVote> CommentVotes => Set<CommentVote>();
    public DbSet<Bookmark> Bookmarks => Set<Bookmark>();
    public DbSet<TopicSubscription> TopicSubscriptions => Set<TopicSubscription>();
    public DbSet<UserFollow> UserFollows => Set<UserFollow>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<ModerationLog> ModerationLogs => Set<ModerationLog>();
    public DbSet<Poll> Polls => Set<Poll>();
    public DbSet<PollOption> PollOptions => Set<PollOption>();
    public DbSet<PollVote> PollVotes => Set<PollVote>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<Badge> Badges => Set<Badge>();
    public DbSet<UserBadge> UserBadges => Set<UserBadge>();
    public DbSet<Draft> Drafts => Set<Draft>();
    public DbSet<UserActivity> UserActivities => Set<UserActivity>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<ShoutMessage> ShoutMessages => Set<ShoutMessage>();
    public DbSet<SiteSetting> SiteSettings => Set<SiteSetting>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<UserWarning> UserWarnings => Set<UserWarning>();
    public DbSet<CmsPage> CmsPages => Set<CmsPage>();
    public DbSet<CategoryModerator> CategoryModerators => Set<CategoryModerator>();
    public DbSet<Reaction> Reactions => Set<Reaction>();
    public DbSet<TagSubscription> TagSubscriptions => Set<TagSubscription>();
    public DbSet<UserNote> UserNotes => Set<UserNote>();
    public DbSet<UserBlock> UserBlocks => Set<UserBlock>();
    public DbSet<PostRevision> PostRevisions => Set<PostRevision>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // ---- Site settings (key-value) ----
        b.Entity<SiteSetting>(e =>
        {
            e.HasKey(s => s.Key);
            e.Property(s => s.Key).HasMaxLength(80);
            e.Property(s => s.Value).HasMaxLength(4000);
        });

        // ---- Tính năng quản trị bổ sung ----
        b.Entity<Announcement>(e =>
        {
            e.Property(a => a.Message).HasMaxLength(500).IsRequired();
            e.Property(a => a.Url).HasMaxLength(500);
        });
        b.Entity<UserWarning>(e => e.Property(w => w.Reason).HasMaxLength(500));
        b.Entity<CmsPage>(e =>
        {
            e.Property(p => p.Slug).HasMaxLength(80).IsRequired();
            e.Property(p => p.Title).HasMaxLength(200).IsRequired();
            e.HasIndex(p => p.Slug).IsUnique();
        });
        b.Entity<CategoryModerator>(e => e.HasKey(cm => new { cm.CategoryId, cm.UserId }));

        // ---- Tính năng tương tác bổ sung ----
        b.Entity<Reaction>(e =>
        {
            e.Property(r => r.Emoji).HasMaxLength(16).IsRequired();
            e.HasIndex(r => new { r.TopicId, r.CommentId });
            e.HasIndex(r => new { r.UserId, r.TopicId, r.CommentId, r.Emoji }).IsUnique();
            e.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId);
            e.HasOne(r => r.Topic).WithMany().HasForeignKey(r => r.TopicId);
            e.HasOne(r => r.Comment).WithMany().HasForeignKey(r => r.CommentId);
        });
        b.Entity<TagSubscription>(e =>
        {
            e.HasKey(x => new { x.UserId, x.TagId });
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Tag).WithMany().HasForeignKey(x => x.TagId);
        });
        b.Entity<UserNote>(e =>
        {
            e.Property(n => n.Body).HasMaxLength(2000).IsRequired();
            e.HasIndex(n => n.UserId);
            e.HasOne(n => n.User).WithMany().HasForeignKey(n => n.UserId);
            e.HasOne(n => n.Author).WithMany().HasForeignKey(n => n.AuthorId);
        });
        b.Entity<UserBlock>(e =>
        {
            e.HasKey(x => new { x.BlockerId, x.BlockedId });
            e.HasOne(x => x.Blocker).WithMany().HasForeignKey(x => x.BlockerId);
            e.HasOne(x => x.Blocked).WithMany().HasForeignKey(x => x.BlockedId);
        });
        b.Entity<PostRevision>(e =>
        {
            e.HasIndex(x => new { x.TargetType, x.TargetId });
            e.Property(x => x.Body).HasMaxLength(20000);
        });
        b.Entity<Category>(e => e.Property(c => c.MinRoleToView).HasMaxLength(20));

        // ---- Identity user ----
        b.Entity<ApplicationUser>(e =>
        {
            e.Property(u => u.DisplayName).HasMaxLength(100);
            e.Property(u => u.Bio).HasMaxLength(1000);
            e.Property(u => u.AvatarUrl).HasMaxLength(512);
            e.Property(u => u.Location).HasMaxLength(120);
        });

        // ---- Category ----
        b.Entity<Category>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(150).IsRequired();
            e.Property(c => c.Slug).HasMaxLength(180).IsRequired();
            e.Property(c => c.IconName).HasMaxLength(64);
            e.Property(c => c.ColorHex).HasMaxLength(9);
            e.HasIndex(c => c.Slug).IsUnique();
            e.HasOne(c => c.ParentCategory).WithMany(c => c.Children).HasForeignKey(c => c.ParentCategoryId);
        });

        // ---- Topic ----
        b.Entity<Topic>(e =>
        {
            e.Property(t => t.Title).HasMaxLength(300).IsRequired();
            e.Property(t => t.Slug).HasMaxLength(320).IsRequired();
            // Soft-delete: lọc mặc định toàn app. Nơi cần thấy bản ghi đã xóa
            // (vd kiểm duyệt khôi phục) dùng IgnoreQueryFilters().
            e.HasQueryFilter(t => !t.IsDeleted);
            e.HasIndex(t => t.Slug);
            e.HasIndex(t => new { t.CategoryId, t.LastActivityAt });
            e.HasIndex(t => t.CreatedAt);
            e.HasIndex(t => t.HotScore);
            e.HasIndex(t => t.Score);
            e.HasOne(t => t.Category).WithMany(c => c.Topics).HasForeignKey(t => t.CategoryId);
            e.HasOne(t => t.Author).WithMany(u => u.Topics).HasForeignKey(t => t.AuthorId);
        });

        // ---- Comment (self-referencing tree) ----
        b.Entity<Comment>(e =>
        {
            e.Property(c => c.Path).HasMaxLength(900);
            // Soft-delete: lọc mặc định. Cây bình luận (GetThreadAsync) cố ý dùng
            // IgnoreQueryFilters() để giữ node đã xóa làm placeholder, không mất reply con.
            e.HasQueryFilter(c => !c.IsDeleted);
            e.HasIndex(c => new { c.TopicId, c.Path });
            e.HasOne(c => c.Topic).WithMany(t => t.Comments).HasForeignKey(c => c.TopicId);
            e.HasOne(c => c.Author).WithMany(u => u.Comments).HasForeignKey(c => c.AuthorId);
            e.HasOne(c => c.ParentComment).WithMany(c => c.Replies).HasForeignKey(c => c.ParentCommentId);
        });

        // ---- Tag + TopicTag (many-to-many) ----
        b.Entity<Tag>(e =>
        {
            e.Property(t => t.Name).HasMaxLength(80).IsRequired();
            e.Property(t => t.Slug).HasMaxLength(100).IsRequired();
            e.HasIndex(t => t.Slug).IsUnique();
        });
        b.Entity<TopicTag>(e =>
        {
            e.HasKey(tt => new { tt.TopicId, tt.TagId });
            e.HasOne(tt => tt.Topic).WithMany(t => t.TopicTags).HasForeignKey(tt => tt.TopicId);
            e.HasOne(tt => tt.Tag).WithMany(t => t.TopicTags).HasForeignKey(tt => tt.TagId);
        });

        // ---- Votes ----
        b.Entity<TopicVote>(e =>
        {
            e.HasIndex(v => new { v.UserId, v.TopicId }).IsUnique();
            e.HasOne(v => v.Topic).WithMany(t => t.Votes).HasForeignKey(v => v.TopicId);
            e.HasOne(v => v.User).WithMany().HasForeignKey(v => v.UserId);
        });
        b.Entity<CommentVote>(e =>
        {
            e.HasIndex(v => new { v.UserId, v.CommentId }).IsUnique();
            e.HasOne(v => v.Comment).WithMany(c => c.Votes).HasForeignKey(v => v.CommentId);
            e.HasOne(v => v.User).WithMany().HasForeignKey(v => v.UserId);
        });

        // ---- Bookmark / Subscription ----
        b.Entity<Bookmark>(e =>
        {
            e.HasKey(x => new { x.UserId, x.TopicId });
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Topic).WithMany(t => t.Bookmarks).HasForeignKey(x => x.TopicId);
        });
        b.Entity<TopicSubscription>(e =>
        {
            e.HasKey(x => new { x.UserId, x.TopicId });
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Topic).WithMany(t => t.Subscriptions).HasForeignKey(x => x.TopicId);
        });

        // ---- UserFollow (two FKs to user) ----
        b.Entity<UserFollow>(e =>
        {
            e.HasKey(x => new { x.FollowerId, x.FolloweeId });
            e.HasOne(x => x.Follower).WithMany(u => u.Following).HasForeignKey(x => x.FollowerId);
            e.HasOne(x => x.Followee).WithMany(u => u.Followers).HasForeignKey(x => x.FolloweeId);
        });

        // ---- Notification ----
        b.Entity<Notification>(e =>
        {
            e.Property(n => n.Message).HasMaxLength(500);
            e.Property(n => n.Url).HasMaxLength(512);
            e.HasIndex(n => new { n.RecipientId, n.IsRead, n.CreatedAt });
            e.HasOne(n => n.Recipient).WithMany().HasForeignKey(n => n.RecipientId);
            e.HasOne(n => n.Actor).WithMany().HasForeignKey(n => n.ActorId);
            e.HasOne(n => n.Topic).WithMany().HasForeignKey(n => n.TopicId);
            e.HasOne(n => n.Comment).WithMany().HasForeignKey(n => n.CommentId);
        });

        // ---- Report ----
        b.Entity<Report>(e =>
        {
            e.Property(r => r.Reason).HasMaxLength(200).IsRequired();
            e.Property(r => r.Details).HasMaxLength(1000);
            e.HasIndex(r => r.Status);
            e.HasOne(r => r.Reporter).WithMany().HasForeignKey(r => r.ReporterId);
            e.HasOne(r => r.ResolvedBy).WithMany().HasForeignKey(r => r.ResolvedById);
            e.HasOne(r => r.Topic).WithMany().HasForeignKey(r => r.TopicId);
            e.HasOne(r => r.Comment).WithMany().HasForeignKey(r => r.CommentId);
        });

        // ---- ModerationLog ----
        b.Entity<ModerationLog>(e =>
        {
            e.Property(m => m.TargetTitle).HasMaxLength(320);
            e.Property(m => m.Detail).HasMaxLength(1000);
            e.HasIndex(m => m.CreatedAt);
            e.HasOne(m => m.Moderator).WithMany().HasForeignKey(m => m.ModeratorId);
        });

        // ---- Poll ----
        b.Entity<Poll>(e =>
        {
            e.Property(p => p.Question).HasMaxLength(300).IsRequired();
            e.HasOne(p => p.Topic).WithOne(t => t.Poll).HasForeignKey<Poll>(p => p.TopicId);
        });
        b.Entity<PollOption>(e =>
        {
            e.Property(o => o.Text).HasMaxLength(200).IsRequired();
            e.HasOne(o => o.Poll).WithMany(p => p.Options).HasForeignKey(o => o.PollId);
        });
        b.Entity<PollVote>(e =>
        {
            e.HasIndex(v => new { v.PollId, v.UserId });
            e.HasOne(v => v.PollOption).WithMany(o => o.Votes).HasForeignKey(v => v.PollOptionId);
            e.HasOne(v => v.User).WithMany().HasForeignKey(v => v.UserId);
        });

        // ---- Attachment ----
        b.Entity<Attachment>(e =>
        {
            e.Property(a => a.FileName).HasMaxLength(260);
            e.Property(a => a.StoredPath).HasMaxLength(400);
            e.Property(a => a.ContentType).HasMaxLength(120);
            e.Property(a => a.AltText).HasMaxLength(300);
            e.HasOne(a => a.Uploader).WithMany().HasForeignKey(a => a.UploaderId);
            e.HasOne(a => a.Topic).WithMany(t => t.Attachments).HasForeignKey(a => a.TopicId);
            e.HasOne(a => a.Comment).WithMany(c => c.Attachments).HasForeignKey(a => a.CommentId);
        });

        // ---- Badge / UserBadge ----
        b.Entity<Badge>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(120).IsRequired();
            e.HasIndex(x => x.Slug).IsUnique();
        });
        b.Entity<UserBadge>(e =>
        {
            e.HasKey(x => new { x.UserId, x.BadgeId });
            e.HasOne(x => x.User).WithMany(u => u.UserBadges).HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Badge).WithMany(bd => bd.UserBadges).HasForeignKey(x => x.BadgeId);
        });

        // ---- Draft / UserActivity ----
        b.Entity<Draft>(e =>
        {
            e.Property(d => d.Title).HasMaxLength(300);
            e.HasIndex(d => d.UserId);
            e.HasOne(d => d.User).WithMany().HasForeignKey(d => d.UserId);
        });
        b.Entity<UserActivity>(e =>
        {
            e.HasIndex(a => new { a.UserId, a.CreatedAt });
            e.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId);
            e.HasOne(a => a.Topic).WithMany().HasForeignKey(a => a.TopicId);
        });

        // ---- Chat ----
        b.Entity<ConversationParticipant>(e =>
        {
            e.HasKey(x => new { x.ConversationId, x.UserId });
            e.HasOne(x => x.Conversation).WithMany(c => c.Participants).HasForeignKey(x => x.ConversationId);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        });
        b.Entity<ShoutMessage>(e =>
        {
            e.Property(m => m.Body).HasMaxLength(500).IsRequired();
            e.HasIndex(m => m.CreatedAt);
            e.HasOne(m => m.Sender).WithMany().HasForeignKey(m => m.SenderId);
        });
        b.Entity<ChatMessage>(e =>
        {
            e.Property(m => m.Body).HasMaxLength(4000).IsRequired();
            e.Property(m => m.AttachmentUrl).HasMaxLength(400);
            e.Property(m => m.AttachmentName).HasMaxLength(260);
            e.Property(m => m.AttachmentType).HasMaxLength(120);
            e.HasIndex(m => new { m.ConversationId, m.CreatedAt });
            e.HasOne(m => m.Conversation).WithMany(c => c.Messages).HasForeignKey(m => m.ConversationId);
            e.HasOne(m => m.Sender).WithMany().HasForeignKey(m => m.SenderId);
        });

        // Trung lập provider: tắt cascade ở mọi quan hệ (tránh multiple-cascade-paths
        // trên SQL Server). Xóa dùng soft-delete trong tầng service.
        foreach (IMutableForeignKey fk in b.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;
    }
}
