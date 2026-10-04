using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ShoeStore.Data;
using ShoeStore.Models;
using System.Text.Json;

namespace ShoeStore.Controllers
{
    [Route("Admin/Products")]
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        // Định dạng ảnh cho phép và dung lượng tối đa (2MB)
        private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private const long MaxImageSize = 2 * 1024 * 1024;

        // Tồn kho từ 1 đến số này được coi là "sắp hết hàng"
        private const int LowStockThreshold = 5;

        // Số sản phẩm trên mỗi trang
        private const int PageSize = 10;

        public ProductsController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index([FromQuery] ProductFilter filter)
        {
            IQueryable<Product> query = _context.Products
                .AsNoTracking()
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .Include(p => p.ProductVariants).ThenInclude(v => v.Size)
                .Include(p => p.ProductVariants).ThenInclude(v => v.Color);

            // ===== Ghép từng điều kiện lọc vào câu truy vấn (chỉ khi người dùng có chọn) =====
            if (!string.IsNullOrWhiteSpace(filter.Keyword))
            {
                var keyword = filter.Keyword.Trim();
                query = query.Where(p => p.Name.Contains(keyword));
            }

            if (filter.BrandId.HasValue)
            {
                query = query.Where(p => p.BrandId == filter.BrandId.Value);
            }

            if (filter.CategoryId.HasValue)
            {
                query = query.Where(p => p.CategoryId == filter.CategoryId.Value);
            }

            // Lọc size/màu: sản phẩm phải có ít nhất 1 biến thể khớp.
            // Chọn cả size và màu thì phải khớp cả hai trên CÙNG một biến thể (vd: size 40 màu Đen).
            if (filter.SizeId.HasValue || filter.ColorId.HasValue)
            {
                var sizeId = filter.SizeId;
                var colorId = filter.ColorId;

                query = query.Where(p => p.ProductVariants.Any(v =>
                    (!sizeId.HasValue || v.SizeId == sizeId.Value) &&
                    (!colorId.HasValue || v.ColorId == colorId.Value)));
            }

            if (filter.Status == "active")
            {
                query = query.Where(p => p.Status);
            }
            else if (filter.Status == "hidden")
            {
                query = query.Where(p => !p.Status);
            }

            if (filter.MinPrice.HasValue)
            {
                query = query.Where(p => p.Price >= filter.MinPrice.Value);
            }

            if (filter.MaxPrice.HasValue)
            {
                query = query.Where(p => p.Price <= filter.MaxPrice.Value);
            }

            if (filter.Stock == "in")
            {
                query = query.Where(p => p.ProductVariants.Sum(v => v.Quantity) > 0);
            }
            else if (filter.Stock == "low")
            {
                query = query.Where(p => p.ProductVariants.Sum(v => v.Quantity) > 0
                                      && p.ProductVariants.Sum(v => v.Quantity) <= LowStockThreshold);
            }
            else if (filter.Stock == "out")
            {
                query = query.Where(p => p.ProductVariants.Sum(v => v.Quantity) == 0);
            }

            // ===== Phân trang =====
            var totalItems = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));

            // Trang không hợp lệ (vd: ?Page=0 hoặc ?Page=999) thì kéo về trong khoảng 1..totalPages
            filter.Page = Math.Clamp(filter.Page, 1, totalPages);

            var products = await query
                .OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.Id)
                .Skip((filter.Page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            // ===== Dữ liệu cho các ô lọc =====
            var brands = await _context.Brands.OrderBy(b => b.Name).ToListAsync();
            var categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
            var sizes = await _context.Sizes.OrderBy(s => s.Name).ToListAsync();
            var colors = await _context.Colors.OrderBy(c => c.Name).ToListAsync();

            ViewBag.FilterBrands = new SelectList(brands, "Id", "Name", filter.BrandId);
            ViewBag.FilterCategories = new SelectList(categories, "Id", "Name", filter.CategoryId);
            ViewBag.FilterSizes = new SelectList(sizes, "Id", "Name", filter.SizeId);
            ViewBag.FilterColors = new SelectList(colors, "Id", "Name", filter.ColorId);

            ViewBag.Filter = filter;
            ViewBag.LowStockThreshold = LowStockThreshold;
            ViewBag.TotalItems = totalItems;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = PageSize;

            return View("~/Views/Admin/Products/Index.cshtml", products);
        }

        [HttpGet("Create")]
        public async Task<IActionResult> Create()
        {
            await LoadDropdowns();

            var product = RestoreFormState() ?? new Product();

            // Form luôn có sẵn ít nhất 1 dòng size/màu
            if (product.ProductVariants.Count == 0)
            {
                product.ProductVariants.Add(new ProductVariant());
            }

            return View("~/Views/Admin/Products/Create.cshtml", product);
        }

        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("Name,Price,Description,Status,BrandId,CategoryId,ProductVariants")] Product product,
            IFormFile? imageFile)
        {
            ValidateImage(imageFile, required: true);
            ValidateVariants(product.ProductVariants);

            if (!ModelState.IsValid)
            {
                SaveFormState(product);
                return RedirectToAction(nameof(Create));
            }

            // Chỉ lấy đúng 3 field cần thiết của mỗi biến thể
            product.ProductVariants = product.ProductVariants
                .Select(v => new ProductVariant
                {
                    SizeId = v.SizeId,
                    ColorId = v.ColorId,
                    Quantity = v.Quantity
                })
                .ToList();

            product.Image = await SaveImage(imageFile!);
            product.CreatedAt = DateTime.Now;

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Thêm sản phẩm thành công!";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("Edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _context.Products
                .AsNoTracking()
                .Include(p => p.ProductVariants)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            await LoadDropdowns();

            // Nếu vừa POST lỗi thì hiển thị lại giá trị người dùng đã nhập
            var posted = RestoreFormState();
            if (posted != null)
            {
                product.Name = posted.Name;
                product.Price = posted.Price;
                product.Description = posted.Description;
                product.Status = posted.Status;
                product.BrandId = posted.BrandId;
                product.CategoryId = posted.CategoryId;
                product.ProductVariants = posted.ProductVariants;
            }

            if (product.ProductVariants.Count == 0)
            {
                product.ProductVariants.Add(new ProductVariant());
            }

            return View("~/Views/Admin/Products/Edit.cshtml", product);
        }

        [HttpPost("Edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("Id,Name,Price,Description,Status,BrandId,CategoryId,ProductVariants")] Product product,
            IFormFile? imageFile)
        {
            if (id != product.Id)
            {
                return NotFound();
            }

            // Lấy sản phẩm gốc kèm các biến thể hiện có trong database
            var existing = await _context.Products
                .Include(p => p.ProductVariants)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (existing == null)
            {
                return NotFound();
            }

            ValidateImage(imageFile, required: false);
            ValidateVariants(product.ProductVariants);

            // Biến thể có trong database nhưng không còn trong form => người dùng đã xoá dòng đó
            var removedVariants = existing.ProductVariants
                .Where(old => !product.ProductVariants.Any(v => v.SizeId == old.SizeId && v.ColorId == old.ColorId))
                .ToList();

            // Không cho xoá biến thể đã có trong đơn hàng
            var removedIds = removedVariants.Select(v => v.Id).ToList();
            if (removedIds.Count > 0 &&
                await _context.OrderDetails.AnyAsync(od => removedIds.Contains(od.ProductVariantId)))
            {
                ModelState.AddModelError("ProductVariants",
                    "Không thể xóa size/màu đã có trong đơn hàng. Hãy đặt số lượng về 0 thay vì xóa.");
            }

            if (!ModelState.IsValid)
            {
                SaveFormState(product);
                return RedirectToAction(nameof(Edit), new { id });
            }

            existing.Name = product.Name;
            existing.Price = product.Price;
            existing.Description = product.Description;
            existing.Status = product.Status;
            existing.BrandId = product.BrandId;
            existing.CategoryId = product.CategoryId;

            // Đồng bộ biến thể: xoá dòng bị bỏ, sửa số lượng dòng cũ, thêm dòng mới
            _context.ProductVariants.RemoveRange(removedVariants);

            foreach (var variant in product.ProductVariants)
            {
                var match = existing.ProductVariants
                    .FirstOrDefault(old => old.SizeId == variant.SizeId && old.ColorId == variant.ColorId);

                if (match != null)
                {
                    match.Quantity = variant.Quantity;
                }
                else
                {
                    existing.ProductVariants.Add(new ProductVariant
                    {
                        SizeId = variant.SizeId,
                        ColorId = variant.ColorId,
                        Quantity = variant.Quantity
                    });
                }
            }

            // Có chọn ảnh mới thì lưu ảnh mới, giữ lại đường dẫn ảnh cũ để xoá sau
            string? oldImage = null;
            if (imageFile != null && imageFile.Length > 0)
            {
                oldImage = existing.Image;
                existing.Image = await SaveImage(imageFile);
            }

            await _context.SaveChangesAsync();

            // Chỉ xoá ảnh cũ sau khi đã lưu database thành công
            DeleteImage(oldImage);

            TempData["SuccessMessage"] = "Sửa sản phẩm thành công!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("ToggleStatus/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id, string? returnUrl)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            product.Status = !product.Status;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = product.Status
                ? $"Đã hiện sản phẩm \"{product.Name}\""
                : $"Đã ẩn sản phẩm \"{product.Name}\"";

            return RedirectToList(returnUrl);
        }

        [HttpPost("Delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, string? returnUrl)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            // Sản phẩm đã có trong đơn hàng thì không cho xoá, vì sẽ làm mất lịch sử đơn hàng
            var hasOrders = await _context.OrderDetails
                .AnyAsync(od => od.ProductVariant!.ProductId == id);

            if (hasOrders)
            {
                TempData["ErrorMessage"] =
                    $"Không thể xóa \"{product.Name}\" vì sản phẩm đã có trong đơn hàng. Hãy dùng chức năng Ẩn.";
                return RedirectToList(returnUrl);
            }

            var imagePath = product.Image;

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            // Xoá file ảnh sau khi đã xoá trong database thành công
            DeleteImage(imagePath);

            TempData["SuccessMessage"] = $"Đã xóa sản phẩm \"{product.Name}\"";
            return RedirectToList(returnUrl);
        }

        // Quay về đúng trang danh sách (kèm bộ lọc + số trang) mà người dùng đang xem.
        // Chỉ chấp nhận đường dẫn nội bộ để tránh bị lợi dụng chuyển hướng sang trang web lạ.
        private IActionResult RedirectToList(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction(nameof(Index));
        }

        // Nạp dữ liệu cho các dropdown: thương hiệu, danh mục, size, màu
        private async Task LoadDropdowns()
        {
            var brands = await _context.Brands.OrderBy(b => b.Name).ToListAsync();
            var categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();

            ViewBag.Brands = new SelectList(brands, "Id", "Name");
            ViewBag.Categories = new SelectList(categories, "Id", "Name");

            ViewBag.Sizes = await _context.Sizes.OrderBy(s => s.Name).ToListAsync();
            ViewBag.Colors = await _context.Colors.OrderBy(c => c.Name).ToListAsync();
        }

        // Kiểm tra danh sách biến thể: phải có ít nhất 1 dòng và không được trùng size + màu
        private void ValidateVariants(ICollection<ProductVariant> variants)
        {
            if (variants.Count == 0)
            {
                ModelState.AddModelError("ProductVariants", "Sản phẩm phải có ít nhất 1 size/màu");
                return;
            }

            var hasDuplicate = variants
                .Where(v => v.SizeId > 0 && v.ColorId > 0)
                .GroupBy(v => new { v.SizeId, v.ColorId })
                .Any(g => g.Count() > 1);

            if (hasDuplicate)
            {
                ModelState.AddModelError("ProductVariants", "Có dòng bị trùng size và màu");
            }
        }

        // Post-Redirect-Get: lưu tạm dữ liệu đã nhập và lỗi validate vào TempData
        private void SaveFormState(Product product)
        {
            TempData["ProductForm"] = JsonSerializer.Serialize(new
            {
                product.Name,
                product.Price,
                product.Description,
                product.Status,
                product.BrandId,
                product.CategoryId,
                ProductVariants = product.ProductVariants.Select(v => new
                {
                    v.SizeId,
                    v.ColorId,
                    v.Quantity
                })
            });
            TempData["ProductErrors"] = JsonSerializer.Serialize(
                ModelState
                    .Where(x => x.Value!.Errors.Count > 0)
                    .ToDictionary(
                        x => x.Key,
                        x => x.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                    )
            );
        }

        // Lấy lại dữ liệu và lỗi từ lần POST trước (TempData chỉ đọc được 1 lần)
        private Product? RestoreFormState()
        {
            Product? product = null;

            if (TempData["ProductForm"] is string formJson)
            {
                product = JsonSerializer.Deserialize<Product>(formJson);
            }

            if (TempData["ProductErrors"] is string errorsJson)
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

            return product;
        }

        private void ValidateImage(IFormFile? imageFile, bool required)
        {
            if (imageFile == null || imageFile.Length == 0)
            {
                if (required)
                {
                    ModelState.AddModelError("ImageFile", "Vui lòng chọn ảnh sản phẩm");
                }
                return;
            }

            var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
            if (!AllowedImageExtensions.Contains(extension))
            {
                ModelState.AddModelError("ImageFile", "Chỉ chấp nhận ảnh .jpg, .jpeg, .png, .webp");
            }

            if (imageFile.Length > MaxImageSize)
            {
                ModelState.AddModelError("ImageFile", "Ảnh không được vượt quá 2MB");
            }
        }

        // Lưu ảnh vào wwwroot/images/products và trả về đường dẫn để lưu vào database
        private async Task<string> SaveImage(IFormFile imageFile)
        {
            var folder = Path.Combine(_env.WebRootPath, "images", "products");
            Directory.CreateDirectory(folder);

            var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(folder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            return $"/images/products/{fileName}";
        }

        // Xoá file ảnh trong wwwroot/images/products (nếu có)
        private void DeleteImage(string? imagePath)
        {
            if (string.IsNullOrEmpty(imagePath) || !imagePath.StartsWith("/images/products/"))
            {
                return;
            }

            var filePath = Path.Combine(_env.WebRootPath, imagePath.TrimStart('/'));

            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }
    }
}