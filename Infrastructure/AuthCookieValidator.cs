using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using ShoeStore.Data;

namespace ShoeStore.Infrastructure
{
    // Mỗi request, kiểm tra lại tài khoản trong cookie còn hợp lệ không.
    // Nhờ vậy khi admin khoá tài khoản hoặc đổi quyền, người dùng đó bị đăng xuất ngay
    // thay vì vẫn dùng được cookie cũ cho tới khi hết hạn.
    public static class AuthCookieValidator
    {
        public static async Task ValidateAsync(CookieValidatePrincipalContext context)
        {
            var principal = context.Principal;
            var idValue = principal?.FindFirstValue(ClaimTypes.NameIdentifier);

            if (principal == null || !int.TryParse(idValue, out var userId))
            {
                await RejectAsync(context);
                return;
            }

            var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();

            var user = await db.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.Status, u.Role })
                .FirstOrDefaultAsync();

            var roleInCookie = principal.FindFirstValue(ClaimTypes.Role);

            if (user == null || !user.Status || user.Role != roleInCookie)
            {
                await RejectAsync(context);
            }
        }

        private static async Task RejectAsync(CookieValidatePrincipalContext context)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}
