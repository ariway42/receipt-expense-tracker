using Npgsql.EntityFrameworkCore.PostgreSQL;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ReceiptExpenseTracker.Data;
using ReceiptExpenseTracker.Models;
using ReceiptExpenseTracker.Repositories;
using ReceiptExpenseTracker.Services;

var builder = WebApplication.CreateBuilder(args);

// ── MVC ───────────────────────────────────────────────────────────────────────
// Global [Authorize] — semua page butuh login kecuali yang [AllowAnonymous]
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.Authorization.AuthorizeFilter());
});

// ── Database ──────────────────────────────────────────────────────────────────
//builder.Services.AddDbContext<ApplicationDbContext>(options =>
//    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
}
else
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
}

// ── Identity ──────────────────────────────────────────────────────────────────
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password rules — sesuaikan kalau mau lebih longgar/ketat
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// ── Cookie (redirect ke login kalau belum auth) ───────────────────────────────
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

// ── Repositories ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<ITransactionItemRepository, TransactionItemRepository>();

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IOcrService, OcrService>();

// ── Upload settings ───────────────────────────────────────────────────────────
builder.Services.Configure<UploadSettings>(
    builder.Configuration.GetSection("UploadSettings"));

var app = builder.Build();

// ── Pipeline ──────────────────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Auto migrate on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

app.UseAuthentication(); // ← WAJIB ada sebelum UseAuthorization
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

public class UploadSettings
{
    public string ReceiptPath { get; set; } = "uploads/receipts";
    public int MaxFileSizeMB { get; set; } = 10;
}