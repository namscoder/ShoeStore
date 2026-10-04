using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ShoeStore.Services;

namespace ShoeStore.ViewComponents
{
    // Cột menu bên trái của khu vực tài khoản (Thông tin, Đổi mật khẩu, Đơn hàng...).
    // ViewComponent tự lấy dữ liệu nên trang nào cũng gắn được, controller không phải truyền gì:
    //   @await Component.InvokeAsync("ProfileNav", new { active = "orders" })
    public class ProfileNavViewComponent : ViewComponent
    {
        private readonly ProfileService _profileService;

        public ProfileNavViewComponent(ProfileService profileService)
        {
            _profileService = profileService;
        }

        // active: "info" | "password" | "orders" => tô đậm mục đang xem
        public async Task<IViewComponentResult> InvokeAsync(string active)
        {
            var idValue = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
            var summary = int.TryParse(idValue, out var userId)
                ? await _profileService.GetSummaryAsync(userId)
                : null;

            if (summary == null)
            {
                return Content(string.Empty);
            }

            ViewBag.Active = active;
            return View(summary);
        }
    }
}
