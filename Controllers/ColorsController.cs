using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShoeStore.Data;
using ShoeStore.Models;
using System.Text.Json;

namespace ShoeStore.Controllers
{
    [Authorize(Roles = AppRoles.Admin)]
    [Route("Admin/Colors")]
    public class ColorsController : Controller
    {
        private readonly ApplicationDbContext _context;
        public ColorsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var colors = await _context.Colors
                .OrderBy(c => c.Id)
                .ToListAsync();

            // Số sản phẩm đang dùng từng màu: trong bảng size/màu HOẶC trong ảnh theo màu.
            // Union tự bỏ cặp (màu, sản phẩm) trùng, nên mỗi sản phẩm chỉ được đếm 1 lần cho 1 màu.
            ViewBag.ProductCounts = await _context.ProductVariants
                .Select(v => new { v.ColorId, v.ProductId })
                .Union(_context.ProductImages
                    .Where(i => i.ColorId != null)
                    .Select(i => new { ColorId = i.ColorId!.Value, i.ProductId }))
                .GroupBy(x => x.ColorId)
                .Select(g => new { ColorId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ColorId, x => x.Count);

            return View("~/Views/Admin/Colors/Index.cshtml", colors);
        }

        [HttpGet("Create")]
        public IActionResult Create()
        {
            var color = RestoreFormState();

            return View("~/Views/Admin/Colors/Create.cshtml", color);
        }

        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name")] Color color)
        {
            color.Name = color.Name?.Trim() ?? string.Empty;
            await ValidateUniqueName(color.Name, null);

            if (!ModelState.IsValid)
            {
                SaveFormState(color);
                return RedirectToAction(nameof(Create));
            }

            _context.Colors.Add(color);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Thêm màu \"{color.Name}\" thành công!";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("Edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var color = await _context.Colors.FindAsync(id);
            if (color == null)
            {
                return NotFound();
            }

            // Nếu vừa POST lỗi thì hiển thị lại giá trị người dùng đã nhập
            var posted = RestoreFormState();
            if (posted != null)
            {
                color.Name = posted.Name;
            }

            return View("~/Views/Admin/Colors/Edit.cshtml", color);
        }

        [HttpPost("Edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name")] Color color)
        {
            if (id != color.Id)
            {
                return NotFound();
            }

            var existing = await _context.Colors.FindAsync(id);
            if (existing == null)
            {
                return NotFound();
            }

            color.Name = color.Name?.Trim() ?? string.Empty;
            await ValidateUniqueName(color.Name, id);

            if (!ModelState.IsValid)
            {
                SaveFormState(color);
                return RedirectToAction(nameof(Edit), new { id });
            }

            existing.Name = color.Name;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Sửa màu \"{existing.Name}\" thành công!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("Delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var color = await _context.Colors.FindAsync(id);
            if (color == null)
            {
                return NotFound();
            }

            // Màu đang được sản phẩm dùng (trong bảng size/màu hoặc ảnh theo màu) thì không cho xoá.
            // Nếu xoá: biến thể của màu này bị xoá theo (Cascade) => mất tồn kho, giỏ hàng, chi tiết đơn hàng;
            // còn ảnh theo màu thì database chặn (NO ACTION) => lỗi sập trang.
            var productCount = await _context.ProductVariants
                .Where(v => v.ColorId == id)
                .Select(v => v.ProductId)
                .Union(_context.ProductImages
                    .Where(i => i.ColorId == id)
                    .Select(i => i.ProductId))
                .CountAsync();

            if (productCount > 0)
            {
                TempData["ErrorMessage"] =
                    $"Không thể xóa màu \"{color.Name}\" vì đang có {productCount} sản phẩm sử dụng. " +
                    "Hãy bỏ màu này khỏi các sản phẩm đó trước.";
                return RedirectToAction(nameof(Index));
            }

            _context.Colors.Remove(color);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã xóa màu \"{color.Name}\"";
            return RedirectToAction(nameof(Index));
        }

        // Không cho trùng tên màu (không phân biệt hoa/thường). excludeId = màu đang sửa (bỏ qua chính nó)
        private async Task ValidateUniqueName(string name, int? excludeId)
        {
            if (string.IsNullOrEmpty(name))
            {
                return; // để [Required] trong Model báo lỗi
            }

            var exists = await _context.Colors
                .AnyAsync(c => c.Name == name && (!excludeId.HasValue || c.Id != excludeId.Value));

            if (exists)
            {
                ModelState.AddModelError("Name", $"Màu \"{name}\" đã tồn tại");
            }
        }

        // Post-Redirect-Get: lưu tạm dữ liệu đã nhập và lỗi validate vào TempData
        // trước khi redirect về GET, để F5 không gửi lại form và lỗi không còn hiển thị
        private void SaveFormState(Color color)
        {
            TempData["ColorForm"] = JsonSerializer.Serialize(new
            {
                color.Name
            });
            TempData["ColorErrors"] = JsonSerializer.Serialize(
                ModelState
                    .Where(x => x.Value!.Errors.Count > 0)
                    .ToDictionary(
                        x => x.Key,
                        x => x.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                    )
            );
        }

        // Lấy lại dữ liệu và lỗi từ lần POST trước (TempData chỉ đọc được 1 lần)
        private Color? RestoreFormState()
        {
            Color? color = null;

            if (TempData["ColorForm"] is string formJson)
            {
                color = JsonSerializer.Deserialize<Color>(formJson);
            }

            if (TempData["ColorErrors"] is string errorsJson)
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

            return color;
        }
    }
}
