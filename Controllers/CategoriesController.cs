using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShoeStore.Data;
using ShoeStore.Models;
using System.Text.Json;

namespace ShoeStore.Controllers
{
    [Authorize(Roles = AppRoles.Admin)]
    [Route("Admin/Categories")]
    public class CategoriesController : Controller
    {
        private readonly ApplicationDbContext _context;
        public CategoriesController(ApplicationDbContext context)
        {
            _context = context;
        }
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var categories = await _context.Categories.ToListAsync();

            return View(
                "~/Views/Admin/Categories/Index.cshtml",
                categories
            );
        }
        [HttpGet("Create")]
        public IActionResult Create()
        {
            var category = RestoreFormState();

            return View(
                    "~/Views/Admin/Categories/Create.cshtml",
                    category
                );
        }
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Category category)
        {
            if (!ModelState.IsValid)
            {
                SaveFormState(category);
                return RedirectToAction(nameof(Create));
            }

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
        [HttpGet("Edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null)
            {
                return NotFound();
            }

            // Nếu vừa POST lỗi thì hiển thị lại giá trị người dùng đã nhập
            var posted = RestoreFormState();
            if (posted != null)
            {
                category.Name = posted.Name;
                category.Description = posted.Description;
            }

            return View("~/Views/Admin/Categories/Edit.cshtml", category);
        }
        [HttpPost("Edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Category category)
        {
            if (id != category.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                SaveFormState(category);
                return RedirectToAction(nameof(Edit), new { id });
            }

            _context.Update(category);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Sửa danh mục thành công!";
            return RedirectToAction(nameof(Index));
        }
        [HttpPost("Delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var category = await _context.Categories.FindAsync(id);

            if (category == null)
            {
                return NotFound();
            }

            // Còn sản phẩm thuộc danh mục này thì không cho xoá
            var productCount = await _context.Products.CountAsync(p => p.CategoryId == id);
            if (productCount > 0)
            {
                TempData["ErrorMessage"] =
                    $"Không thể xóa danh mục \"{category.Name}\" vì đang có {productCount} sản phẩm. " +
                    "Hãy chuyển các sản phẩm sang danh mục khác trước.";
                return RedirectToAction(nameof(Index));
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Xóa danh mục thành công!";

            return RedirectToAction(nameof(Index));
        }
        // Post-Redirect-Get: lưu tạm dữ liệu đã nhập và lỗi validate vào TempData
        // trước khi redirect về GET, để F5 không gửi lại form và lỗi không còn hiển thị
        private void SaveFormState(Category category)
        {
            TempData["CategoryForm"] = JsonSerializer.Serialize(new
            {
                category.Name,
                category.Description
            });
            TempData["CategoryErrors"] = JsonSerializer.Serialize(
                ModelState
                    .Where(x => x.Value!.Errors.Count > 0)
                    .ToDictionary(
                        x => x.Key,
                        x => x.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                    )
            );
        }

        // Lấy lại dữ liệu và lỗi từ lần POST trước (TempData chỉ đọc được 1 lần)
        private Category? RestoreFormState()
        {
            Category? category = null;

            if (TempData["CategoryForm"] is string formJson)
            {
                category = JsonSerializer.Deserialize<Category>(formJson);
            }

            if (TempData["CategoryErrors"] is string errorsJson)
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

            return category;
        }
    }
}
