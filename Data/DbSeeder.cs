using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShoeStore.Models;

namespace ShoeStore.Data
{
    // Tạo dữ liệu ban đầu khi chạy ứng dụng.
    // Hiện tại: tạo 1 tài khoản Admin nếu trong database chưa có Admin nào.
    public static class DbSeeder
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();

            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

            await db.Database.MigrateAsync();

            if (await db.Users.AnyAsync(u => u.Role == AppRoles.Admin))
            {
                return;
            }

            // Thông tin admin lấy từ appsettings.json (mục "SeedAdmin")
            var username = config["SeedAdmin:Username"] ?? "admin";
            var email = config["SeedAdmin:Email"] ?? "admin@shoestore.local";
            var password = config["SeedAdmin:Password"];

            if (string.IsNullOrWhiteSpace(password))
            {
                logger.LogWarning(
                "Chưa cấu hình SeedAdmin:Password nên chưa tạo tài khoản Admin. " +
                    "Hãy chạy: dotnet user-secrets set \"SeedAdmin:Password\" \"<mật khẩu>\"");
                return;
            }
            if (await db.Users.AnyAsync(u => u.Username == username || u.Email == email))
            {
                logger.LogWarning(
                    "Không tạo được tài khoản Admin mặc định vì username '{Username}' hoặc email '{Email}' đã tồn tại.",
                    username, email);
                return;
            }

            var admin = new User
            {
                Username = username,
                Email = email,
                FullName = "Quản trị viên",
                Role = AppRoles.Admin,
                Status = true,
                CreatedAt = DateTime.Now
            };
            admin.PasswordHash = hasher.HashPassword(admin, password);

            db.Users.Add(admin);
            await db.SaveChangesAsync();

            logger.LogInformation("Đã tạo tài khoản Admin mặc định: {Username}", username);
        }
    }
}
