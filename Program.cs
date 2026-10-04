using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShoeStore.Data;
using ShoeStore.Infrastructure;
using ShoeStore.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));

// Băm / kiểm tra mật khẩu (PBKDF2, có salt) - không bao giờ lưu mật khẩu gốc
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// Đăng nhập bằng cookie
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";               // Chưa đăng nhập -> chuyển tới đây
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied"; // Không đủ quyền -> chuyển tới đây

        options.Cookie.Name = "ShoeStore.Auth";
        options.Cookie.HttpOnly = true;                     // JavaScript không đọc được cookie
        options.Cookie.SameSite = SameSiteMode.Lax;

        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;                   // Còn hoạt động thì tự gia hạn

        // Tài khoản bị khoá / đổi quyền thì đăng xuất ngay
        options.Events.OnValidatePrincipal = AuthCookieValidator.ValidateAsync;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Tạo tài khoản Admin mặc định nếu chưa có
await DbSeeder.SeedAsync(app.Services);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication(); // Phải đứng trước UseAuthorization
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
