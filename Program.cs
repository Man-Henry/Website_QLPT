using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Website_QLPT.Data;
using Website_QLPT.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false; // Tắt yêu cầu xác nhận email
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

// Cấu hình cookie đăng nhập: 30 ngày khi tick "Ghi nhớ", bảo mật HttpOnly + SameSite
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(30);     // Persistent cookie sống 30 ngày
    options.SlidingExpiration = true;                    // Tự gia hạn khi user còn hoạt động
    options.Cookie.HttpOnly = true;                      // Chặn JS đọc cookie (chống XSS)
    options.Cookie.SameSite = SameSiteMode.Lax;         // Bảo vệ CSRF cơ bản
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // HTTPS trên prod
    options.Cookie.Name = ".QLPT.Auth";                 // Tên cookie dễ nhận biết
});

builder.Services.AddTransient<SmtpEmailSender>();
builder.Services.AddTransient<IEmailSenderService>(sp => sp.GetRequiredService<SmtpEmailSender>());
builder.Services.AddTransient<IEmailSender>(sp => sp.GetRequiredService<SmtpEmailSender>());
builder.Services.AddScoped<ICurrentTenantService, CurrentTenantService>();

// Add Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Website_QLPT API",
        Version = "v1",
        Description = "Public API endpoints cho hệ thống Quản Lý Phòng Trọ"
    });
});

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseSwagger();
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Website_QLPT API V1");
    });
}

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages(); // Required for Identity UI pages

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await Website_QLPT.Data.SeedData.Initialize(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred seeding the DB.");
    }
}

app.Run();
