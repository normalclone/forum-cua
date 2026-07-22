using Forum.Web.Data;
using Forum.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Services
// ---------------------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? "Data Source=forum.db";

// EF Core. Dùng SQLite cho phát triển; đổi sang UseSqlServer + connection string
// để chuyển sang SQL Server (tầng dữ liệu giữ trung lập provider).
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

// ASP.NET Core Identity (khóa int, vai trò tùy biến).
builder.Services
    .AddIdentity<ApplicationUser, ApplicationRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;

        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false; // demo: đăng nhập ngay, không cần xác nhận email
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Kiểm tra lại security stamp mỗi 2 phút: khi admin khóa/cấm hoặc đổi vai trò (kèm
// UpdateSecurityStampAsync), phiên đăng nhập đang mở của người đó sẽ bị vô hiệu trong ≤2 phút
// thay vì tồn tại tới khi cookie hết hạn (14 ngày).
builder.Services.Configure<Microsoft.AspNetCore.Identity.SecurityStampValidatorOptions>(
    o => o.ValidationInterval = TimeSpan.FromMinutes(2));

// Thêm claim DisplayName/Avatar/Reputation vào cookie.
builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, Forum.Web.Services.AppClaimsPrincipalFactory>();

// Antiforgery qua header để AJAX gửi token.
builder.Services.AddAntiforgery(o => o.HeaderName = "RequestVerificationToken");

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/dang-nhap";
    options.LogoutPath = "/dang-xuat";
    options.AccessDeniedPath = "/tu-choi-truy-cap";
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
});

builder.Services.AddControllersWithViews();
// Presence (ChatHub.Online) + định tuyến group là per-process — đúng cho deploy ĐƠN
// instance hiện tại. Khi scale-out nhiều instance phải thêm backplane:
// .AddSignalR().AddStackExchangeRedis(...) hoặc .AddAzureSignalR(...).
builder.Services.AddSignalR();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();

// ---- Tầng service ----
builder.Services.AddSingleton<Forum.Web.Services.ISlugService, Forum.Web.Services.SlugService>();
builder.Services.AddSingleton<Forum.Web.Services.IMarkdownService, Forum.Web.Services.MarkdownService>();
builder.Services.AddSingleton<Forum.Web.Services.ISiteSettingService, Forum.Web.Services.SiteSettingService>();
builder.Services.AddSingleton<Forum.Web.Services.IWordFilterService, Forum.Web.Services.WordFilterService>();
builder.Services.AddScoped<Forum.Web.Services.IForumUrlService, Forum.Web.Services.ForumUrlService>();
builder.Services.AddScoped<Forum.Web.Services.ISeoService, Forum.Web.Services.SeoService>();
builder.Services.AddScoped<Forum.Web.Services.INotificationService, Forum.Web.Services.NotificationService>();
builder.Services.AddScoped<Forum.Web.Services.IReputationService, Forum.Web.Services.ReputationService>();
builder.Services.AddScoped<Forum.Web.Services.IVoteService, Forum.Web.Services.VoteService>();
builder.Services.AddScoped<Forum.Web.Services.IEngagementService, Forum.Web.Services.EngagementService>();
builder.Services.AddScoped<Forum.Web.Services.IPostingGuardService, Forum.Web.Services.PostingGuardService>();
builder.Services.AddScoped<Forum.Web.Services.ISearchService, Forum.Web.Services.SearchService>();
builder.Services.AddScoped<Forum.Web.Services.IModerationService, Forum.Web.Services.ModerationService>();
builder.Services.AddScoped<Forum.Web.Services.ITopicService, Forum.Web.Services.TopicService>();
builder.Services.AddScoped<Forum.Web.Services.ICommentService, Forum.Web.Services.CommentService>();
builder.Services.AddScoped<Forum.Web.Data.Seed.SeedService>();

var app = builder.Build();

// ---------------------------------------------------------------------------
// Migrate + seed dữ liệu khi khởi chạy
// ---------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<Forum.Web.Data.Seed.SeedService>();
    await seeder.SeedAsync();
}

// ---------------------------------------------------------------------------
// HTTP pipeline
// ---------------------------------------------------------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/loi");
    app.UseHsts();
    // Chỉ redirect HTTPS ngoài Development: profile "http" (5080) không bind cổng https
    // nên gọi ở Development chỉ sinh warning "Failed to determine the https port" vô ích.
    app.UseHttpsRedirection();
}

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Tệp do người dùng tải lên (/uploads): không cho trình duyệt sniff kiểu MIME
        // và luôn tải về thay vì render inline → chặn stored-XSS từ nội dung tải lên.
        // Ảnh nhúng trong bài (<img src="/uploads/...">) vẫn hiển thị bình thường vì
        // Content-Disposition không áp dụng cho subresource.
        if (ctx.Context.Request.Path.StartsWithSegments("/uploads"))
        {
            ctx.Context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            ctx.Context.Response.Headers["Content-Disposition"] = "attachment";
        }
    }
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<Forum.Web.Hubs.ForumHub>("/hubs/forum");
app.MapHub<Forum.Web.Hubs.ChatHub>("/hubs/chat");

app.Run();

// Cho phép WebApplicationFactory trong test E2E truy cập lớp Program.
public partial class Program { }
