using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShoeStore.Data;
using ShoeStore.Models;
using ShoeStore.Models.ViewModels;

// Trong Controller, "User" là người đang đăng nhập (ClaimsPrincipal),
// nên đặt tên khác cho class User trong Models để khỏi nhầm
using AppUser = ShoeStore.Models.User;

namespace ShoeStore.Controllers
{
    [Authorize(Roles = AppRoles.Admin)]
    [Route("Admin/Users")]
    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<AppUser> _passwordHasher;

        // Số tài khoản trên mỗi trang
        private const int PageSize = 10;

        // Các vai trò được phép chọn
        private static readonly string[] AllowedRoles = { AppRoles.Customer, AppRoles.Admin };

        public UsersController(ApplicationDbContext context, IPasswordHasher<AppUser> passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
        }

        // Id của admin đang đăng nhập
        private int CurrentUserId =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

        [HttpGet("")]
        public async Task<IActionResult> Index([FromQuery] UserFilter filter)
        {
            var query = _context.Users.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.Keyword))
            {
                var keyword = filter.Keyword.Trim();
                query = query.Where(u =>
                    u.Username.Contains(keyword) ||
                    u.Email.Contains(keyword) ||
                    (u.FullName != null && u.FullName.Contains(keyword)) ||
                    (u.Phone != null && u.Phone.Contains(keyword)));
            }

            if (!string.IsNullOrEmpty(filter.Role) && AllowedRoles.Contains(filter.Role))
            {
                query = query.Where(u => u.Role == filter.Role);
            }

            if (filter.Status == "active")
            {
                query = query.Where(u => u.Status);
            }
            else if (filter.Status == "locked")
            {
                query = query.Where(u => !u.Status);
            }

            // Phân trang
            var totalItems = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
            filter.Page = Math.Clamp(filter.Page, 1, totalPages);

            var users = await query
                .OrderBy(u => u.Id)
                .Skip((filter.Page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            ViewBag.Filter = filter;
            ViewBag.TotalItems = totalItems;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = PageSize;
            ViewBag.CurrentUserId = CurrentUserId;

            return View("~/Views/Admin/Users/Index.cshtml", users);
        }

        [HttpGet("Create")]
        public IActionResult Create()
        {
            var model = RestoreFormState() ?? new UserFormViewModel();
            model.Id = 0;

            return View("~/Views/Admin/Users/Create.cshtml", model);
        }

        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserFormViewModel model)
        {
            Normalize(model);

            if (string.IsNullOrEmpty(model.Password))
            {
                ModelState.AddModelError(nameof(model.Password), "Vui lòng nhập mật khẩu");
            }

            ValidateRole(model.Role);
            await ValidateUniqueAsync(model, excludeId: null);

            if (!ModelState.IsValid)
            {
                SaveFormState(model);
                return RedirectToAction(nameof(Create));
            }

            var user = new AppUser
            {
                Username = model.Username,
                Email = model.Email,
                FullName = model.FullName,
                Phone = model.Phone,
                Address = model.Address,
                Role = model.Role,
                Status = true,
                CreatedAt = DateTime.Now
            };
            user.PasswordHash = _passwordHasher.HashPassword(user, model.Password!);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã tạo tài khoản \"{user.Username}\" ({RoleName(user.Role)})";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("Edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                return NotFound();
            }

            // Nếu vừa POST lỗi thì hiển thị lại giá trị đã nhập, không thì lấy từ database
            var model = RestoreFormState() ?? new UserFormViewModel
            {
                Email = user.Email,
                FullName = user.FullName ?? string.Empty,
                Phone = user.Phone,
                Address = user.Address,
                Role = user.Role
            };
            model.Id = user.Id;
            model.Username = user.Username;

            ViewBag.IsSelf = user.Id == CurrentUserId;
            ViewBag.OriginalRole = user.Role;
            ViewBag.Status = user.Status;
            ViewBag.CreatedAt = user.CreatedAt;

            return View("~/Views/Admin/Users/Edit.cshtml", model);
        }

        [HttpPost("Edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, UserFormViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            // Tên đăng nhập không cho sửa => luôn lấy từ database, bỏ qua kiểm tra
            ModelState.Remove(nameof(model.Username));
            model.Username = user.Username;

            Normalize(model);
            ValidateRole(model.Role);
            await ValidateUniqueAsync(model, excludeId: id);

            // Hạ quyền một Admin xuống Khách hàng
            if (user.Role == AppRoles.Admin && model.Role != AppRoles.Admin)
            {
                if (user.Id == CurrentUserId)
                {
                    ModelState.AddModelError(nameof(model.Role), "Bạn không thể tự hạ quyền của chính mình");
                }
                else if (user.Status && await IsLastActiveAdminAsync(user.Id))
                {
                    ModelState.AddModelError(nameof(model.Role),
                        "Đây là quản trị viên cuối cùng đang hoạt động, không thể hạ quyền");
                }
            }

            if (!ModelState.IsValid)
            {
                SaveFormState(model);
                return RedirectToAction(nameof(Edit), new { id });
            }

            user.Email = model.Email;
            user.FullName = model.FullName;
            user.Phone = model.Phone;
            user.Address = model.Address;
            user.Role = model.Role;

            // Có nhập mật khẩu mới => đặt lại mật khẩu
            var passwordReset = !string.IsNullOrEmpty(model.Password);
            if (passwordReset)
            {
                user.PasswordHash = _passwordHasher.HashPassword(user, model.Password!);
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã cập nhật tài khoản \"{user.Username}\"" +
                (passwordReset ? ". Đã đặt lại mật khẩu: hãy báo khách đăng nhập rồi vào Tài khoản → Đổi mật khẩu để tự đặt mật khẩu mới." : "");
            return RedirectToAction(nameof(Index));
        }

        // Khoá / mở khoá tài khoản (thay cho Xoá, để không mất lịch sử đơn hàng)
        [HttpPost("ToggleStatus/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id, string? returnUrl)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            // Chỉ cần kiểm tra khi KHOÁ (mở khoá thì luôn được)
            if (user.Status)
            {
                string? error = null;

                if (user.Id == CurrentUserId)
                {
                    error = "Bạn không thể tự khoá tài khoản của chính mình";
                }
                else if (user.Role == AppRoles.Admin && await IsLastActiveAdminAsync(user.Id))
                {
                    error = "Đây là quản trị viên cuối cùng đang hoạt động, không thể khoá";
                }

                if (error != null)
                {
                    TempData["ErrorMessage"] = error;
                    return RedirectToList(returnUrl);
                }
            }

            user.Status = !user.Status;
            await _context.SaveChangesAsync();

            // Người bị khoá sẽ bị đăng xuất ngay ở lần tải trang kế tiếp (AuthCookieValidator)
            TempData["SuccessMessage"] = user.Status
                ? $"Đã mở khoá tài khoản \"{user.Username}\""
                : $"Đã khoá tài khoản \"{user.Username}\"";

            return RedirectToList(returnUrl);
        }

        // ===================== Hàm hỗ trợ =====================

        // Không còn Admin nào khác đang hoạt động (ngoài tài khoản excludeId)
        private async Task<bool> IsLastActiveAdminAsync(int excludeId)
        {
            return !await _context.Users
                .AnyAsync(u => u.Role == AppRoles.Admin && u.Status && u.Id != excludeId);
        }

        // Bỏ khoảng trắng thừa; ô tuỳ chọn để trống thì lưu null
        private static void Normalize(UserFormViewModel model)
        {
            model.Username = model.Username?.Trim() ?? string.Empty;
            model.Email = model.Email?.Trim() ?? string.Empty;
            model.FullName = model.FullName?.Trim() ?? string.Empty;
            model.Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim();
            model.Address = string.IsNullOrWhiteSpace(model.Address) ? null : model.Address.Trim();
        }

        private void ValidateRole(string role)
        {
            if (!AllowedRoles.Contains(role))
            {
                ModelState.AddModelError(nameof(UserFormViewModel.Role), "Vai trò không hợp lệ");
            }
        }

        // Không trùng tên đăng nhập / email với tài khoản khác (excludeId = tài khoản đang sửa)
        private async Task ValidateUniqueAsync(UserFormViewModel model, int? excludeId)
        {
            if (!string.IsNullOrEmpty(model.Username) && await _context.Users
                    .AnyAsync(u => u.Username == model.Username && (!excludeId.HasValue || u.Id != excludeId.Value)))
            {
                ModelState.AddModelError(nameof(model.Username), "Tên đăng nhập đã được sử dụng");
            }

            if (!string.IsNullOrEmpty(model.Email) && await _context.Users
                    .AnyAsync(u => u.Email == model.Email && (!excludeId.HasValue || u.Id != excludeId.Value)))
            {
                ModelState.AddModelError(nameof(model.Email), "Email đã được sử dụng");
            }
        }

        private static string RoleName(string role) =>
            role == AppRoles.Admin ? "Quản trị viên" : "Khách hàng";

        // Quay về đúng trang danh sách (kèm bộ lọc + số trang). Chỉ chấp nhận đường dẫn nội bộ.
        private IActionResult RedirectToList(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction(nameof(Index));
        }

        // Post-Redirect-Get: lưu tạm dữ liệu đã nhập và lỗi vào TempData.
        // KHÔNG lưu mật khẩu (TempData nằm trong cookie) => khi lỗi, ô mật khẩu sẽ trống.
        private void SaveFormState(UserFormViewModel model)
        {
            TempData["UserForm"] = JsonSerializer.Serialize(new
            {
                model.Username,
                model.Email,
                model.FullName,
                model.Phone,
                model.Address,
                model.Role
            });
            TempData["UserErrors"] = JsonSerializer.Serialize(
                ModelState
                    .Where(x => x.Value!.Errors.Count > 0)
                    .ToDictionary(
                        x => x.Key,
                        x => x.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                    )
            );
        }

        // Lấy lại dữ liệu và lỗi từ lần POST trước (TempData chỉ đọc được 1 lần)
        private UserFormViewModel? RestoreFormState()
        {
            UserFormViewModel? model = null;

            if (TempData["UserForm"] is string formJson)
            {
                model = JsonSerializer.Deserialize<UserFormViewModel>(formJson);
            }

            if (TempData["UserErrors"] is string errorsJson)
            {
                var errors = JsonSerializer.Deserialize<Dictionary<string, string[]>>(errorsJson);
                if (errors != null)
                {
                    foreach (var (key, messages) in errors)
                    {
                        foreach (var message in messages)
                        {
                            ModelState.AddModelError(key, message);
                        }
                    }
                }
            }

            return model;
        }
    }
}
