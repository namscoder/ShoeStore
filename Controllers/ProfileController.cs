using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShoeStore.Data;
using ShoeStore.Infrastructure;
using ShoeStore.Models;
using ShoeStore.Models.ViewModels;
using ShoeStore.Services;

namespace ShoeStore.Controllers
{
    // Trang tài khoản cá nhân: xem tổng quan, sửa thông tin + địa chỉ mặc định, đổi mật khẩu
    [Authorize]
    [Route("tai-khoan")]
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly ProfileService _profileService;

        public ProfileController(ApplicationDbContext context, IPasswordHasher<User> passwordHasher, ProfileService profileService)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _profileService = profileService;
        }

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // ===================== Thông tin tài khoản =====================

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == CurrentUserId);
            if (user == null)
            {
                return NotFound();
            }

            // Lần trước gửi form bị lỗi thì lấy lại dữ liệu đã nhập, không thì điền từ tài khoản
            var fromUser = FromUser(user);
            var model = RestoreProfileForm() ?? fromUser;
            model.SavedAddressHint = fromUser.SavedAddressHint;
            model.Summary = await _profileService.GetSummaryAsync(user.Id);

            return View(model);
        }

        [HttpPost("")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(ProfileViewModel model)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == CurrentUserId);
            if (user == null)
            {
                return NotFound();
            }

            model.FullName = model.FullName?.Trim() ?? string.Empty;
            model.Email = model.Email?.Trim() ?? string.Empty;
            model.Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim();

            var address = BuildAddress(model, user.Address);

            // Email phải không trùng với tài khoản KHÁC
            if (!string.IsNullOrEmpty(model.Email) &&
                await _context.Users.AnyAsync(u => u.Email == model.Email && u.Id != user.Id))
            {
                ModelState.AddModelError(nameof(model.Email), "Email đã được tài khoản khác sử dụng");
            }

            if (!ModelState.IsValid)
            {
                SaveProfileForm(model);
                return RedirectToAction(nameof(Index));
            }

            user.FullName = model.FullName;
            user.Email = model.Email;
            user.Phone = model.Phone;
            user.Address = address;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // 2 người cùng đổi sang 1 email cùng lúc: unique index trong database chặn lại
                ModelState.AddModelError(nameof(model.Email), "Email đã được tài khoản khác sử dụng");
                SaveProfileForm(model);
                return RedirectToAction(nameof(Index));
            }

            // Tên và email nằm trong cookie đăng nhập (vd: "Xin chào, ..." trên header) nên cần cấp lại cookie
            await RefreshSignInAsync(user);

            TempData["SuccessMessage"] = "Đã cập nhật thông tin tài khoản";
            return RedirectToAction(nameof(Index));
        }

        // ===================== Đổi mật khẩu =====================

        [HttpGet("doi-mat-khau")]
        public IActionResult ChangePassword()
        {
            // Mật khẩu KHÔNG BAO GIỜ lưu vào TempData, chỉ lấy lại câu báo lỗi
            RestoreErrors("PasswordErrors");

            return View(new ChangePasswordViewModel());
        }

        [HttpPost("doi-mat-khau")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == CurrentUserId);
            if (user == null)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.CurrentPassword);

                if (result == PasswordVerificationResult.Failed)
                {
                    ModelState.AddModelError(nameof(model.CurrentPassword), "Mật khẩu hiện tại không đúng");
                }
                else if (model.NewPassword == model.CurrentPassword)
                {
                    ModelState.AddModelError(nameof(model.NewPassword), "Mật khẩu mới phải khác mật khẩu hiện tại");
                }
            }

            if (!ModelState.IsValid)
            {
                SaveErrors("PasswordErrors");
                return RedirectToAction(nameof(ChangePassword));
            }

            user.PasswordHash = _passwordHasher.HashPassword(user, model.NewPassword);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đổi mật khẩu thành công. Lần đăng nhập sau hãy dùng mật khẩu mới.";
            return RedirectToAction(nameof(ChangePassword));
        }

        // ===================== Hàm hỗ trợ =====================

        // Kiểm tra địa chỉ và trả về chuỗi sẽ lưu vào User.Address (null = không có địa chỉ).
        // Địa chỉ là KHÔNG BẮT BUỘC: để trống hết thì xoá địa chỉ, đã điền thì phải điền đủ.
        private string? BuildAddress(ProfileViewModel model, string? currentAddress)
        {
            if (model.AddressMode == "manual")
            {
                model.Address = model.Address?.Trim();

                if (string.IsNullOrEmpty(model.Address))
                {
                    return null;
                }

                if (model.Address.Length < 10)
                {
                    ModelState.AddModelError(nameof(model.Address), "Vui lòng nhập địa chỉ đầy đủ (ít nhất 10 ký tự)");
                }

                return model.Address;
            }

            model.AddressMode = "picker";
            model.ProvinceName = model.ProvinceName?.Trim();
            model.WardName = model.WardName?.Trim();
            model.Street = model.Street?.Trim();

            var nothingFilled = string.IsNullOrEmpty(model.ProvinceName)
                && string.IsNullOrEmpty(model.WardName)
                && string.IsNullOrEmpty(model.Street);

            if (nothingFilled)
            {
                // Địa chỉ cũ kiểu nhập tay (không chọn lại được trên danh sách) thì giữ nguyên,
                // tránh trường hợp chỉ sửa tên mà bị mất địa chỉ
                var isOldFormat = !string.IsNullOrWhiteSpace(currentAddress)
                    && !AddressFormatter.TrySplit(currentAddress, out _, out _, out _);

                return isOldFormat ? currentAddress : null;
            }

            if (string.IsNullOrEmpty(model.ProvinceName))
            {
                ModelState.AddModelError(nameof(model.ProvinceName), "Vui lòng chọn tỉnh/thành phố");
            }

            if (string.IsNullOrEmpty(model.WardName))
            {
                ModelState.AddModelError(nameof(model.WardName), "Vui lòng chọn phường/xã");
            }

            if (string.IsNullOrEmpty(model.Street) || model.Street.Length < 3)
            {
                ModelState.AddModelError(nameof(model.Street), "Vui lòng nhập số nhà, tên đường");
            }

            var address = AddressFormatter.Join(model.Street ?? "", model.WardName ?? "", model.ProvinceName ?? "");

            if (address.Length > 255)
            {
                ModelState.AddModelError(nameof(model.Street), "Địa chỉ quá dài, hãy rút gọn số nhà, tên đường");
            }

            return address;
        }

        // Điền sẵn form từ tài khoản
        private static ProfileViewModel FromUser(User user)
        {
            var model = new ProfileViewModel
            {
                FullName = user.FullName ?? string.Empty,
                Email = user.Email,
                Phone = user.Phone,
                // Nếu phải chuyển sang nhập tay thì ô nhập tay có sẵn địa chỉ hiện tại
                Address = user.Address
            };

            if (AddressFormatter.TrySplit(user.Address, out var province, out var ward, out var street))
            {
                model.ProvinceName = province;
                model.WardName = ward;
                model.Street = street;
            }
            else if (!string.IsNullOrWhiteSpace(user.Address))
            {
                model.SavedAddressHint = user.Address;
            }

            return model;
        }

        // Cấp lại cookie đăng nhập với tên / email mới (giữ nguyên lựa chọn "Ghi nhớ đăng nhập").
        // Các claim giống hệt AccountController.SignInAsync
        private async Task RefreshSignInAsync(User user)
        {
            var current = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

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
                new AuthenticationProperties { IsPersistent = current.Properties?.IsPersistent ?? false });
        }

        // ===== Post-Redirect-Get: lưu tạm dữ liệu đã nhập và lỗi vào TempData =====

        private void SaveProfileForm(ProfileViewModel model)
        {
            TempData["ProfileForm"] = JsonSerializer.Serialize(new
            {
                model.FullName,
                model.Email,
                model.Phone,
                model.ProvinceName,
                model.WardName,
                model.Street,
                model.Address,
                model.AddressMode
            });
            SaveErrors("ProfileErrors");
        }

        private ProfileViewModel? RestoreProfileForm()
        {
            ProfileViewModel? model = null;

            if (TempData["ProfileForm"] is string json)
            {
                model = JsonSerializer.Deserialize<ProfileViewModel>(json);
            }

            RestoreErrors("ProfileErrors");
            return model;
        }

        private void SaveErrors(string key)
        {
            TempData[key] = JsonSerializer.Serialize(
                ModelState
                    .Where(x => x.Value!.Errors.Count > 0)
                    .ToDictionary(
                        x => x.Key,
                        x => x.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                    )
            );
        }

        private void RestoreErrors(string key)
        {
            if (TempData[key] is not string json)
            {
                return;
            }

            var errors = JsonSerializer.Deserialize<Dictionary<string, string[]>>(json);
            if (errors == null)
            {
                return;
            }

            foreach (var (field, messages) in errors)
            {
                foreach (var message in messages)
                {
                    ModelState.AddModelError(field, message);
                }
            }
        }
    }
}
