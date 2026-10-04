using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShoeStore.Data;
using ShoeStore.Models;
using ShoeStore.Models.ViewModels;

namespace ShoeStore.Controllers
{
    // Trang danh sách sản phẩm cho khách: tìm kiếm, lọc, sắp xếp, phân trang
    [Route("san-pham")]
    public class ShopController : Controller
    {
        private readonly ApplicationDbContext _context;

        private const int PageSize = 12;

        // So sánh chuỗi không phân biệt hoa/thường và DẤU: gõ "giay" vẫn ra "Giày"
        private const string AccentInsensitive = "Vietnamese_CI_AI";

        public ShopController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index([FromQuery] ShopFilter filter)
        {
            // Chỉ sản phẩm đang bán
            IQueryable<Product> query = _context.Products
                .AsNoTracking()
                .Where(p => p.Status);

            // ===== Tìm kiếm =====
            if (!string.IsNullOrWhiteSpace(filter.Q))
            {
                var keyword = filter.Q.Trim();
                query = query.Where(p =>
                    EF.Functions.Collate(p.Name, AccentInsensitive).Contains(keyword) ||
                    EF.Functions.Collate(p.Brand!.Name, AccentInsensitive).Contains(keyword) ||
                    EF.Functions.Collate(p.Category!.Name, AccentInsensitive).Contains(keyword));
            }

            // ===== Lọc =====
            if (filter.CategoryId.HasValue)
            {
                query = query.Where(p => p.CategoryId == filter.CategoryId.Value);
            }

            if (filter.BrandIds.Any())
            {
                var brandIds = filter.BrandIds;
                query = query.Where(p => brandIds.Contains(p.BrandId));
            }

            if (filter.SizeIds.Any())
            {
                // Có ít nhất 1 biến thể đúng size (và còn hàng nếu tick "Còn hàng")
                var sizeIds = filter.SizeIds;
                var inStock = filter.InStock;
                query = query.Where(p => p.ProductVariants.Any(v =>
                    sizeIds.Contains(v.SizeId) && (!inStock || v.Quantity > 0)));
            }
            else if (filter.InStock)
            {
                query = query.Where(p => p.ProductVariants.Any(v => v.Quantity > 0));
            }

            if (filter.MinPrice.HasValue)
            {
                query = query.Where(p => p.Price >= filter.MinPrice.Value);
            }

            if (filter.MaxPrice.HasValue)
            {
                query = query.Where(p => p.Price <= filter.MaxPrice.Value);
            }

            // ===== Sắp xếp =====
            if (!ShopFilter.SortOptions.ContainsKey(filter.Sort ?? ""))
            {
                filter.Sort = "newest";
            }

            IOrderedQueryable<Product> ordered = filter.Sort switch
            {
                "price_asc" => query.OrderBy(p => p.Price),
                "price_desc" => query.OrderByDescending(p => p.Price),
                "name" => query.OrderBy(p => p.Name),
                // Bán chạy: tổng số đôi đã bán (bỏ đơn đã huỷ)
                "bestseller" => query.OrderByDescending(p => _context.OrderDetails
                    .Where(d => d.ProductVariant!.ProductId == p.Id && d.Order!.Status != OrderStatuses.Cancelled)
                    .Sum(d => (int?)d.Quantity) ?? 0),
                _ => query.OrderByDescending(p => p.CreatedAt)
            };

            // ===== Phân trang =====
            var totalItems = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
            filter.Page = Math.Clamp(filter.Page, 1, totalPages);

            var products = await ordered
                .ThenByDescending(p => p.Id)
                .Skip((filter.Page - 1) * PageSize)
                .Take(PageSize)
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .Include(p => p.ProductVariants).ThenInclude(v => v.Size)
                .Include(p => p.ProductVariants).ThenInclude(v => v.Color)
                .AsSplitQuery()
                .ToListAsync();

            // ===== Dữ liệu cho cột lọc (chỉ những mục đang có sản phẩm bán) =====
            var categories = await _context.Categories
                .AsNoTracking()
                .Select(c => new ShopFacet { Id = c.Id, Name = c.Name, Count = c.Products.Count(p => p.Status) })
                .Where(c => c.Count > 0)
                .OrderBy(c => c.Name)
                .ToListAsync();

            var brands = await _context.Brands
                .AsNoTracking()
                .Select(b => new ShopFacet { Id = b.Id, Name = b.Name, Count = b.Products.Count(p => p.Status) })
                .Where(b => b.Count > 0)
                .OrderBy(b => b.Name)
                .ToListAsync();

            // Size đang có ở sản phẩm đang bán, sắp theo số: 35, 35.5, 36...
            var sizes = (await _context.Sizes
                    .AsNoTracking()
                    .Where(s => s.ProductVariants.Any(v => v.Product!.Status))
                    .ToListAsync())
                .OrderBy(s => decimal.TryParse(s.Name, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : decimal.MaxValue)
                .ThenBy(s => s.Name)
                .ToList();

            var model = new ShopViewModel
            {
                Filter = filter,
                Products = products,
                TotalItems = totalItems,
                TotalPages = totalPages,
                PageSize = PageSize,
                Categories = categories,
                Brands = brands,
                Sizes = sizes
            };

            return View(model);
        }
    }
}
