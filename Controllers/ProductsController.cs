using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ShoeStore.Data;
using ShoeStore.Infrastructure;
using ShoeStore.Models;
using System.Text.Json;

namespace ShoeStore.Controllers
{
    [Authorize(Roles = AppRoles.Admin)]
    [Route("Admin/Products")]
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        // Định dạng ảnh cho phép và dung lượng tối đa (2MB)
        private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private const long MaxImageSize = 2 * 1024 * 1024;

        // Thư viện ảnh: tối đa số ảnh mỗi nhóm (ảnh chung / mỗi màu) và tổng dung lượng 1 lần gửi
        private const int MaxGalleryImagesPerGroup = 8;
        private const long MaxGalleryTotalSize = 50 * 1024 * 1024;

        // Giới hạn kích thước request khi thêm/sửa (lớn hơn tổng ảnh một chút cho các ô chữ)
        private const long MaxProductRequestSize = 60 * 1024 * 1024;

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
                .Include(p => p.ProductVariants).ThenInclude(v => v.Color)
                .Include(p => p.Images).ThenInclude(i => i.Color)
                // Lấy 2 danh sách (biến thể + ảnh) bằng các câu SQL riêng thay vì 1 câu JOIN rất to
                .AsSplitQuery();

            query = ApplyFilter(query, filter);

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

        // ===================== Xuất Excel =====================

        // GET /Admin/Products/Export?...: xuất các sản phẩm ĐANG LỌC (không phân trang), gồm 2 sheet:
        // Sản phẩm (mỗi sản phẩm 1 dòng) | Tồn kho (mỗi size + màu 1 dòng)
        [HttpGet("Export")]
        public async Task<IActionResult> Export([FromQuery] ProductFilter filter)
        {
            IQueryable<Product> query = _context.Products
                .AsNoTracking()
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .Include(p => p.ProductVariants).ThenInclude(v => v.Size)
                .Include(p => p.ProductVariants).ThenInclude(v => v.Color)
                .AsSplitQuery();

            var products = await ApplyFilter(query, filter)
                .OrderBy(p => p.Name)
                .ThenBy(p => p.Id)
                .ToListAsync();

            // Đã bán (không tính đơn huỷ) theo từng sản phẩm, 1 câu SQL
            var productIds = products.Select(p => p.Id).ToList();
            var soldByProduct = await _context.OrderDetails
                .Where(d => d.Order!.Status != OrderStatuses.Cancelled && productIds.Contains(d.ProductVariant!.ProductId))
                .GroupBy(d => d.ProductVariant!.ProductId)
                .Select(g => new { ProductId = g.Key, Sold = g.Sum(d => d.Quantity) })
                .ToDictionaryAsync(x => x.ProductId, x => x.Sold);

            string StockLabel(int quantity) => quantity == 0 ? "Hết hàng"
                : quantity <= LowStockThreshold ? "Sắp hết" : "Còn hàng";

            var book = new XlsxWorkbook();

            // ----- Sheet 1: Sản phẩm -----
            var productSheet = book.AddSheet("Sản phẩm", new[] { 7d, 36d, 14d, 16d, 14d, 11d, 10d, 12d, 10d, 12d });
            productSheet.AddHeader("Mã SP", "Tên sản phẩm", "Thương hiệu", "Danh mục", "Giá bán", "Trạng thái",
                "Tồn kho", "Tình trạng", "Đã bán", "Ngày tạo");
            foreach (var p in products)
            {
                var stock = p.ProductVariants.Sum(v => v.Quantity);
                productSheet.AddRow(
                    XlsxCell.Number(p.Id),
                    p.Name,
                    p.Brand?.Name,
                    p.Category?.Name,
                    XlsxCell.Money(p.Price),
                    p.Status ? "Đang bán" : "Đang ẩn",
                    XlsxCell.Number(stock),
                    StockLabel(stock),
                    XlsxCell.Number(soldByProduct.GetValueOrDefault(p.Id)),
                    XlsxCell.Date(p.CreatedAt));
            }
            productSheet.AddRow(
                XlsxCell.Text($"Tổng: {products.Count} sản phẩm", XlsxStyle.Bold),
                XlsxCell.Text("", XlsxStyle.Bold), XlsxCell.Text("", XlsxStyle.Bold), XlsxCell.Text("", XlsxStyle.Bold),
                XlsxCell.Text("", XlsxStyle.Bold), XlsxCell.Text("", XlsxStyle.Bold),
                XlsxCell.Number(products.Sum(p => p.ProductVariants.Sum(v => v.Quantity)), XlsxStyle.BoldInteger),
                XlsxCell.Text("", XlsxStyle.Bold),
                XlsxCell.Number(soldByProduct.Values.Sum(), XlsxStyle.BoldInteger),
                XlsxCell.Text("", XlsxStyle.Bold));

            // ----- Sheet 2: Tồn kho theo size + màu -----
            var stockSheet = book.AddSheet("Tồn kho theo size-màu", new[] { 7d, 36d, 14d, 14d, 8d, 10d, 12d });
            stockSheet.AddHeader("Mã SP", "Tên sản phẩm", "Thương hiệu", "Màu", "Size", "Số lượng", "Tình trạng");
            foreach (var p in products)
            {
                // Size là chữ ("39", "40.5") nên sắp xếp theo giá trị số
                var variants = p.ProductVariants
                    .OrderBy(v => v.Color?.Name)
                    .ThenBy(v => double.TryParse(v.Size?.Name, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var size) ? size : double.MaxValue);

                foreach (var v in variants)
                {
                    stockSheet.AddRow(
                        XlsxCell.Number(p.Id),
                        p.Name,
                        p.Brand?.Name,
                        v.Color?.Name,
                        v.Size?.Name,
                        XlsxCell.Number(v.Quantity),
                        StockLabel(v.Quantity));
                }
            }

            var fileName = $"san-pham-ton-kho_{DateTime.Now:yyyyMMdd-HHmm}.xlsx";
            return File(book.ToBytes(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // Ghép các điều kiện lọc vào câu truy vấn (dùng chung cho trang danh sách và xuất Excel)
        private static IQueryable<Product> ApplyFilter(IQueryable<Product> query, ProductFilter filter)
        {
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

            return query;
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
        [RequestSizeLimit(MaxProductRequestSize)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxProductRequestSize)]
        public async Task<IActionResult> Create(
            [Bind("Name,Price,Description,Status,BrandId,CategoryId,ProductVariants")] Product product,
            IFormFile? imageFile,
            List<ProductImageGroup>? imageGroups)
        {
            imageGroups ??= new List<ProductImageGroup>();

            ValidateImage(imageFile, required: true);
            ValidateVariants(product.ProductVariants);
            ValidateImageGroups(imageGroups, product.ProductVariants);

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
            await AddGalleryImages(product, imageGroups);
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
                .Include(p => p.Images).ThenInclude(i => i.Color)
                .AsSplitQuery()
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
        [RequestSizeLimit(MaxProductRequestSize)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxProductRequestSize)]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("Id,Name,Price,Description,Status,BrandId,CategoryId,ProductVariants")] Product product,
            IFormFile? imageFile,
            List<ProductImageGroup>? imageGroups,
            List<int>? deleteImageIds)
        {
            if (id != product.Id)
            {
                return NotFound();
            }

            imageGroups ??= new List<ProductImageGroup>();
            deleteImageIds ??= new List<int>();

            // Lấy sản phẩm gốc kèm các biến thể và ảnh hiện có trong database
            var existing = await _context.Products
                .Include(p => p.ProductVariants)
                .Include(p => p.Images).ThenInclude(i => i.Color)
                .AsSplitQuery()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (existing == null)
            {
                return NotFound();
            }

            // Ảnh bị đánh dấu xoá: chỉ lấy ảnh THUỘC sản phẩm này (không tin Id gửi từ form)
            var imagesToDelete = existing.Images.Where(i => deleteImageIds.Contains(i.Id)).ToList();
            var keptImages = existing.Images.Except(imagesToDelete).ToList();

            ValidateImage(imageFile, required: false);
            ValidateVariants(product.ProductVariants);
            ValidateImageGroups(imageGroups, product.ProductVariants, keptImages);

            // Không cho bỏ một màu khỏi bảng size/màu khi màu đó vẫn còn ảnh (chưa đánh dấu xoá)
            var variantColorIds = product.ProductVariants.Select(v => v.ColorId).ToHashSet();
            var orphanColors = keptImages
                .Where(i => i.ColorId.HasValue && !variantColorIds.Contains(i.ColorId.Value))
                .Select(i => i.Color?.Name)
                .Distinct()
                .ToList();

            if (orphanColors.Count > 0)
            {
                ModelState.AddModelError("ImageGroups",
                    $"Màu {string.Join(", ", orphanColors)} vẫn còn ảnh. " +
                    "Hãy xoá ảnh của màu đó trước khi bỏ màu khỏi bảng size/màu.");
            }

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

            // Có chọn ảnh đại diện mới thì lưu ảnh mới, giữ lại đường dẫn ảnh cũ để xoá sau
            string? oldImage = null;
            if (imageFile != null && imageFile.Length > 0)
            {
                oldImage = existing.Image;
                existing.Image = await SaveImage(imageFile);
            }

            // Thư viện ảnh: xoá các ảnh bị đánh dấu, rồi thêm ảnh mới (nối tiếp thứ tự ảnh cũ cùng màu)
            foreach (var image in imagesToDelete)
            {
                existing.Images.Remove(image);
                _context.ProductImages.Remove(image);
            }
            await AddGalleryImages(existing, imageGroups);

            await _context.SaveChangesAsync();

            // Chỉ xoá file ảnh sau khi đã lưu database thành công
            DeleteImage(oldImage);
            foreach (var image in imagesToDelete)
            {
                DeleteImage(image.ImageUrl);
            }

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
            var product = await _context.Products
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id);

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

            // Ảnh đại diện + toàn bộ ảnh trong thư viện (các dòng ProductImages tự xoá theo nhờ Cascade)
            var imagePaths = new List<string?> { product.Image };
            imagePaths.AddRange(product.Images.Select(i => i.ImageUrl));

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            // Xoá file ảnh sau khi đã xoá trong database thành công
            foreach (var path in imagePaths)
            {
                DeleteImage(path);
            }

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

            // Giới hạn thư viện ảnh, gửi sang view để JavaScript kiểm tra giống server
            ViewBag.MaxGalleryImagesPerGroup = MaxGalleryImagesPerGroup;
            ViewBag.MaxGalleryTotalSize = MaxGalleryTotalSize;
            ViewBag.MaxImageSize = MaxImageSize;
        }

        // Kiểm tra thư viện ảnh: đúng định dạng/dung lượng, không quá số ảnh mỗi nhóm (tính cả ảnh cũ còn giữ),
        // và màu của nhóm ảnh phải có trong bảng size/màu của sản phẩm
        private void ValidateImageGroups(
            List<ProductImageGroup> groups,
            ICollection<ProductVariant> variants,
            IEnumerable<ProductImage>? keptImages = null)
        {
            keptImages ??= Enumerable.Empty<ProductImage>();

            var variantColorIds = variants
                .Where(v => v.ColorId > 0)
                .Select(v => v.ColorId)
                .ToHashSet();

            long totalSize = 0;

            foreach (var group in groups)
            {
                var files = group.Files.Where(f => f.Length > 0).ToList();
                if (files.Count == 0)
                {
                    continue;
                }

                if (group.ColorId.HasValue && !variantColorIds.Contains(group.ColorId.Value))
                {
                    ModelState.AddModelError("ImageGroups", "Có ảnh thuộc màu không có trong bảng size/màu");
                }

                var existingCount = keptImages.Count(i => i.ColorId == group.ColorId);
                if (existingCount + files.Count > MaxGalleryImagesPerGroup)
                {
                    ModelState.AddModelError("ImageGroups", $"Mỗi nhóm ảnh tối đa {MaxGalleryImagesPerGroup} ảnh");
                }

                foreach (var file in files)
                {
                    var error = GetImageError(file);
                    if (error != null)
                    {
                        ModelState.AddModelError("ImageGroups", $"Ảnh \"{file.FileName}\": {error}");
                    }

                    totalSize += file.Length;
                }
            }

            if (totalSize > MaxGalleryTotalSize)
            {
                ModelState.AddModelError("ImageGroups", "Tổng dung lượng thư viện ảnh quá lớn");
            }
        }

        // Lưu file ảnh của từng nhóm và gắn vào sản phẩm.
        // SortOrder nối tiếp các ảnh đã có cùng màu (dùng lại được cho trang Sửa).
        private async Task AddGalleryImages(Product product, List<ProductImageGroup> groups)
        {
            foreach (var group in groups)
            {
                var nextOrder = product.Images
                    .Where(i => i.ColorId == group.ColorId)
                    .Select(i => i.SortOrder + 1)
                    .DefaultIfEmpty(0)
                    .Max();

                foreach (var file in group.Files.Where(f => f.Length > 0))
                {
                    product.Images.Add(new ProductImage
                    {
                        ColorId = group.ColorId,
                        ImageUrl = await SaveImage(file),
                        SortOrder = nextOrder++
                    });
                }
            }
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

            var error = GetImageError(imageFile);
            if (error != null)
            {
                ModelState.AddModelError("ImageFile", error);
            }
        }

        // Kiểm tra 1 file ảnh: sai định dạng hoặc quá dung lượng thì trả về câu báo lỗi, hợp lệ thì trả về null
        private static string? GetImageError(IFormFile file)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedImageExtensions.Contains(extension))
            {
                return "Chỉ chấp nhận ảnh .jpg, .jpeg, .png, .webp";
            }

            if (file.Length > MaxImageSize)
            {
                return "Ảnh không được vượt quá 2MB";
            }

            return null;
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