using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShoeStore.Data;
using ShoeStore.Models;

namespace ShoeStore.Controllers
{
    public class HomeController : Controller
    {
        private const int NewProductCount = 8;

        private const int BestSellerCount = 4;

        private const int RelatedProductCount = 4;

        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Sản phẩm mới nhất đang bán, kèm thương hiệu, danh mục, size, màu
            var newProducts = await _context.Products
                .AsNoTracking()
                .Where(p => p.Status)
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .Include(p => p.ProductVariants).ThenInclude(v => v.Size)
                .Include(p => p.ProductVariants).ThenInclude(v => v.Color)
                .OrderByDescending(p => p.CreatedAt)
                .Take(NewProductCount)
                .AsSplitQuery()
                .ToListAsync();

            // Sản phẩm bán chạy: cộng số lượng đã bán trong chi tiết đơn hàng (bỏ qua đơn đã huỷ)
            var soldByProduct = await _context.OrderDetails
                .AsNoTracking()
                .Where(d => d.Order!.Status != "Cancelled" && d.ProductVariant!.Product!.Status)
                .GroupBy(d => d.ProductVariant!.ProductId)
                .Select(g => new { ProductId = g.Key, Sold = g.Sum(d => d.Quantity) })
                .OrderByDescending(x => x.Sold)
                .Take(BestSellerCount)
                .ToListAsync();

            var bestSellerIds = soldByProduct.Select(x => x.ProductId).ToList();

            var bestSellerProducts = await _context.Products
                .AsNoTracking()
                .Where(p => bestSellerIds.Contains(p.Id))
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .Include(p => p.ProductVariants).ThenInclude(v => v.Size)
                .Include(p => p.ProductVariants).ThenInclude(v => v.Color)
                .AsSplitQuery()
                .ToListAsync();

            // Giữ đúng thứ tự bán nhiều -> ít
            var bestSellers = soldByProduct
                .Join(bestSellerProducts, s => s.ProductId, p => p.Id,
                    (s, p) => new BestSellerItem { Product = p, SoldQuantity = s.Sold })
                .ToList();

            var categories = await _context.Categories
                .AsNoTracking()
                .Select(c => new HomeGroupItem
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    ProductCount = c.Products.Count(p => p.Status)
                })
                .OrderByDescending(c => c.ProductCount)
                .ThenBy(c => c.Name)
                .ToListAsync();

            var brands = await _context.Brands
                .AsNoTracking()
                .Select(b => new HomeGroupItem
                {
                    Id = b.Id,
                    Name = b.Name,
                    Description = b.Description,
                    ProductCount = b.Products.Count(p => p.Status)
                })
                .OrderByDescending(b => b.ProductCount)
                .ThenBy(b => b.Name)
                .ToListAsync();

            var model = new HomeViewModel
            {
                NewProducts = newProducts,
                BestSellers = bestSellers,
                Categories = categories,
                Brands = brands,
                TotalProducts = await _context.Products.CountAsync(p => p.Status)
            };

            return View(model);
        }

        // Trang thương hiệu: /thuong-hieu/5 -> tất cả sản phẩm đang bán của thương hiệu đó
        [Route("thuong-hieu/{id:int}")]
        public async Task<IActionResult> Brand(int id)
        {
            var brand = await _context.Brands
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == id);

            if (brand == null)
            {
                return NotFound();
            }

            var products = await _context.Products
                .AsNoTracking()
                .Where(p => p.Status && p.BrandId == id)
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .Include(p => p.ProductVariants).ThenInclude(v => v.Size)
                .Include(p => p.ProductVariants).ThenInclude(v => v.Color)
                .OrderByDescending(p => p.CreatedAt)
                .AsSplitQuery()
                .ToListAsync();

            var otherBrands = await _context.Brands
                .AsNoTracking()
                .Where(b => b.Id != id)
                .OrderBy(b => b.Name)
                .Select(b => new HomeGroupItem
                {
                    Id = b.Id,
                    Name = b.Name,
                    ProductCount = b.Products.Count(p => p.Status)
                })
                .ToListAsync();

            var model = new BrandPageViewModel
            {
                Brand = brand,
                Products = products,
                OtherBrands = otherBrands
            };

            return View(model);
        }

        // Trang chi tiết sản phẩm: /san-pham/5
        [Route("san-pham/{id:int}")]
        public async Task<IActionResult> Detail(int id)
        {
            var product = await ProductsWithDetails()
                .FirstOrDefaultAsync(p => p.Id == id && p.Status);

            // Không có hoặc đang ẩn thì báo 404
            if (product == null)
            {
                return NotFound();
            }

            // Tổng số đôi đã bán (bỏ qua đơn đã huỷ)
            var soldQuantity = await _context.OrderDetails
                .Where(d => d.ProductVariant!.ProductId == id && d.Order!.Status != "Cancelled")
                .SumAsync(d => (int?)d.Quantity) ?? 0;

            // Sản phẩm liên quan: ưu tiên cùng danh mục, thiếu thì lấy thêm cùng thương hiệu
            var related = await ProductsWithDetails()
                .Where(p => p.Status && p.Id != id && p.CategoryId == product.CategoryId)
                .OrderByDescending(p => p.CreatedAt)
                .Take(RelatedProductCount)
                .ToListAsync();

            if (related.Count < RelatedProductCount)
            {
                var takenIds = related.Select(p => p.Id).ToList();

                var sameBrand = await ProductsWithDetails()
                    .Where(p => p.Status && p.Id != id && p.BrandId == product.BrandId && !takenIds.Contains(p.Id))
                    .OrderByDescending(p => p.CreatedAt)
                    .Take(RelatedProductCount - related.Count)
                    .ToListAsync();

                related.AddRange(sameBrand);
            }

            var model = new ProductDetailViewModel
            {
                Product = product,
                SoldQuantity = soldQuantity,
                RelatedProducts = related
            };

            return View(model);
        }

        // Truy vấn sản phẩm kèm thương hiệu, danh mục, size, màu (dùng cho trang chi tiết)
        private IQueryable<Product> ProductsWithDetails()
        {
            return _context.Products
                .AsNoTracking()
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .Include(p => p.ProductVariants).ThenInclude(v => v.Size)
                .Include(p => p.ProductVariants).ThenInclude(v => v.Color)
                .AsSplitQuery();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}
