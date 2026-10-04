using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShoeStore.Data;
using ShoeStore.Models;
using ShoeStore.Models.ViewModels;

namespace ShoeStore.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;

        public AccountController(ApplicationDbContext context, IPasswordHasher<User> passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
        }

        // ===================== ĐĂNG NHẬP =====================

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            // Đã đăng nhập rồi thì không cần vào trang đăng nhập nữa
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectAfterLogin(returnUrl);
            }

            ViewBag.ReturnUrl = returnUrl;
            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var input = model.UsernameOrEmail.Trim();

            // Cho phép đăng nhập bằng tên đăng nhập hoặc email
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == input || u.Email == input);

            // Sai tên đăng nhập hay sai mật khẩu đều báo chung 1 câu,
            // để người khác không dò được tài khoản nào có tồn tại
            var result = user == null
                ? PasswordVerificationResult.Failed
                : _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);

            if (user == null || result == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc mật khẩu không đúng");
                return View(model);
            }

            if (!user.Status)
            {
                ModelState.AddModelError(string.Empty, "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ cửa hàng.");
                return View(model);
            }

            // Thuật toán băm được nâng cấp thì băm lại mật khẩu theo chuẩn mới
            if (result == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);
                await _context.SaveChangesAsync();
            }

            await SignInAsync(user, model.RememberMe);

            return RedirectAfterLogin(returnUrl, user.Role);
        }

        // ===================== QUÊN MẬT KHẨU =====================

        // Chưa gửi email tự động: hướng dẫn khách liên hệ cửa hàng,
        // admin xác minh rồi đặt lại mật khẩu trong trang Quản trị > Người dùng > Sửa
        [HttpGet("quen-mat-khau")]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        // ===================== ĐĂNG KÝ =====================

        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }

            return View(new RegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            // Ô bỏ trống sẽ được gửi lên là null nên cần "?."
            model.Username = model.Username?.Trim() ?? string.Empty;
            model.Email = model.Email?.Trim() ?? string.Empty;
            model.FullName = model.FullName?.Trim() ?? string.Empty;
            model.Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim();

            // Kiểm tra trùng tên đăng nhập / email trước khi lưu
            if (await _context.Users.AnyAsync(u => u.Username == model.Username))
            {
                ModelState.AddModelError(nameof(model.Username), "Tên đăng nhập đã được sử dụng");
            }

            if (await _context.Users.AnyAsync(u => u.Email == model.Email))
            {
                ModelState.AddModelError(nameof(model.Email), "Email đã được sử dụng");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = new User
            {
                Username = model.Username,
                Email = model.Email,
                FullName = model.FullName,
                Phone = model.Phone,
                Role = AppRoles.Customer, // Người tự đăng ký luôn là khách hàng
                Status = true,
                CreatedAt = DateTime.Now
            };
            user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);

            _context.Users.Add(user);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Trường hợp 2 người đăng ký cùng lúc cùng 1 username/email:
                // bước kiểm tra ở trên đều qua, nhưng unique index trong database chặn lại
                ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc email đã được sử dụng");
                return View(model);
            }

            // Đăng ký xong thì đăng nhập luôn
            await SignInAsync(user, isPersistent: false);

            TempData["SuccessMessage"] = $"Chào mừng {user.FullName} đến với ShoeStore!";
            return RedirectToAction("Index", "Home");
        }

        // ===================== ĐĂNG XUẤT =====================

        // Dùng POST (không dùng link GET) để trang web khác không thể tự đăng xuất người dùng
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        // Đã đăng nhập nhưng không đủ quyền (vd: khách hàng vào trang /Admin)
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        // ===================== HÀM PHỤ =====================

        // Tạo cookie đăng nhập chứa thông tin cơ bản của người dùng
        private async Task SignInAsync(User user, bool isPersistent)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("FullName", user.FullName ?? user.Username)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties
                {
                    // Ghi nhớ đăng nhập: cookie vẫn còn sau khi tắt trình duyệt
                    IsPersistent = isPersistent
                });
        }

        // Sau khi đăng nhập: quay lại trang đang xem dở (nếu là link nội bộ),
        // không có thì Admin vào trang quản trị, khách hàng về trang chủ
        private IActionResult RedirectAfterLogin(string? returnUrl, string? role = null)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            role ??= User.FindFirstValue(ClaimTypes.Role);

            if (role == AppRoles.Admin)
            {
                return RedirectToAction("Index", "Admin");
            }

            return RedirectToAction("Index", "Home");
        }
    }
}
