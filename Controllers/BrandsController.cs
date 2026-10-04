using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShoeStore.Data;
using ShoeStore.Models;
using System.Text.Json;

namespace ShoeStore.Controllers
{
    [Route("Admin/Brands")]
    public class BrandsController : Controller
    {
        private readonly ApplicationDbContext _context;
        public BrandsController(ApplicationDbContext context)
        {
            _context = context;
        }
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var brands = await _context.Brands.ToListAsync();

            return View(
                "~/Views/Admin/Brands/Index.cshtml",
                brands
            );
        }
        [HttpGet("Create")]
        public IActionResult Create()
        {
            var brand = RestoreFormState();

            return View(
                    "~/Views/Admin/Brands/Create.cshtml",
                    brand
                );
        }
        [HttpPost("Create")]
        public async Task<IActionResult> Create(Brand brand)
        {
            if (!ModelState.IsValid)
            {
                SaveFormState(brand);
                return RedirectToAction(nameof(Create));
            }

            _context.Brands.Add(brand);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Thêm thương hiệu thành công!";
            return RedirectToAction(nameof(Index));
        }
        [HttpGet("Edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var brand = await _context.Brands.FindAsync(id);
            if (brand == null)
            {
                return NotFound();
            }

            // Nếu vừa POST lỗi thì hiển thị lại giá trị người dùng đã nhập
            var posted = RestoreFormState();
            if (posted != null)
            {
                brand.Name = posted.Name;
                brand.Description = posted.Description;
            }

            return View("~/Views/Admin/Brands/Edit.cshtml", brand);
        }
        [HttpPost("Edit/{id}")]
        public async Task<IActionResult> Edit(int id, Brand brand)
        {
            if (id != brand.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                SaveFormState(brand);
                return RedirectToAction(nameof(Edit), new { id });
            }

            _context.Update(brand);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Sửa thương hiệu thành công!";
            return RedirectToAction(nameof(Index));
        }
        [HttpPost("Delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var brand = await _context.Brands.FindAsync(id);

            if (brand == null)
            {
                return NotFound();
            }

            // Còn sản phẩm thuộc thương hiệu này thì không cho xoá
            var productCount = await _context.Products.CountAsync(p => p.BrandId == id);
            if (productCount > 0)
            {
                TempData["ErrorMessage"] =
                    $"Không thể xóa thương hiệu \"{brand.Name}\" vì đang có {productCount} sản phẩm. " +
                    "Hãy chuyển các sản phẩm sang thương hiệu khác trước.";
                return RedirectToAction(nameof(Index));
            }

            _context.Brands.Remove(brand);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Xóa thương hiệu thành công!";

            return RedirectToAction(nameof(Index));
        }
        // Post-Redirect-Get: lưu tạm dữ liệu đã nhập và lỗi validate vào TempData
        // trước khi redirect về GET, để F5 không gửi lại form và lỗi không còn hiển thị
        private void SaveFormState(Brand brand)
        {
            TempData["BrandForm"] = JsonSerializer.Serialize(new
            {
                brand.Name,
                brand.Description
            });
            TempData["BrandErrors"] = JsonSerializer.Serialize(
                ModelState
                    .Where(x => x.Value!.Errors.Count > 0)
                    .ToDictionary(
                        x => x.Key,
                        x => x.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                    )
            );
        }

        // Lấy lại dữ liệu và lỗi từ lần POST trước (TempData chỉ đọc được 1 lần)
        private Brand? RestoreFormState()
        {
            Brand? brand = null;

            if (TempData["BrandForm"] is string formJson)
            {
                brand = JsonSerializer.Deserialize<Brand>(formJson);
            }

            if (TempData["BrandErrors"] is string errorsJson)
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

            return brand;
        }
    }
}
