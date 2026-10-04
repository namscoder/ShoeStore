using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShoeStore.Data;
using ShoeStore.Models;
using ShoeStore.Models.ViewModels;

namespace ShoeStore.Controllers
{
    [Authorize(Roles = AppRoles.Admin)]
    public class AdminController : Controller
    {
        // Giống ngưỡng "sắp hết hàng" ở trang Sản phẩm (ProductsController)
        private const int LowStockThreshold = 5;

        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Dashboard: số liệu thật lấy từ database
        [Route("Admin")]
        public async Task<IActionResult> Index()
        {
            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var lastMonthStart = monthStart.AddMonths(-1);
            var chartStart = today.AddDays(-13); // 14 ngày gần nhất, tính cả hôm nay

            var orders = _context.Orders.AsNoTracking();
            var completed = orders.Where(o => o.Status == OrderStatuses.Completed);

            var model = new DashboardViewModel
            {
                LowStockThreshold = LowStockThreshold,

                // ----- Doanh thu (đơn hoàn thành, tính theo ngày đặt) -----
                RevenueThisMonth = await completed.Where(o => o.OrderDate >= monthStart).SumAsync(o => o.TotalAmount),
                RevenueLastMonth = await completed.Where(o => o.OrderDate >= lastMonthStart && o.OrderDate < monthStart).SumAsync(o => o.TotalAmount),
                RevenueTotal = await completed.SumAsync(o => o.TotalAmount),

                // ----- Đơn hàng -----
                OrdersToday = await orders.CountAsync(o => o.OrderDate >= today),
                OrdersThisMonth = await orders.CountAsync(o => o.OrderDate >= monthStart),
                StatusCounts = await orders
                    .GroupBy(o => o.Status)
                    .Select(g => new { g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Key, x => x.Count),

                // ----- Khách hàng -----
                CustomersTotal = await _context.Users.CountAsync(u => u.Role == AppRoles.Customer),
                NewCustomersThisMonth = await _context.Users.CountAsync(u => u.Role == AppRoles.Customer && u.CreatedAt >= monthStart),

                // ----- Sản phẩm (đếm theo tổng tồn kho của tất cả size/màu) -----
                TotalProducts = await _context.Products.CountAsync(),
                ActiveProducts = await _context.Products.CountAsync(p => p.Status),
                OutOfStockCount = await _context.Products.CountAsync(p => p.ProductVariants.Sum(v => v.Quantity) == 0),
                LowStockCount = await _context.Products.CountAsync(p =>
                    p.ProductVariants.Sum(v => v.Quantity) > 0 && p.ProductVariants.Sum(v => v.Quantity) <= LowStockThreshold),
            };

            // ----- Biểu đồ 14 ngày: số đơn + giá trị đơn đặt mỗi ngày (bỏ đơn huỷ) -----
            var byDay = await orders
                .Where(o => o.OrderDate >= chartStart && o.Status != OrderStatuses.Cancelled)
                .GroupBy(o => o.OrderDate.Date)
                .Select(g => new { Date = g.Key, Orders = g.Count(), Amount = g.Sum(o => o.TotalAmount) })
                .ToListAsync();

            // Ngày không có đơn vẫn phải có cột (bằng 0)
            model.Last14Days = Enumerable.Range(0, 14)
                .Select(i => chartStart.AddDays(i))
                .Select(d => new DashboardDay
                {
                    Date = d,
                    Orders = byDay.FirstOrDefault(x => x.Date == d)?.Orders ?? 0,
                    Amount = byDay.FirstOrDefault(x => x.Date == d)?.Amount ?? 0
                })
                .ToList();

            // ----- Đơn mới nhất -----
            model.RecentOrders = await orders
                .Include(o => o.User)
                .OrderByDescending(o => o.OrderDate)
                .Take(6)
                .ToListAsync();

            // ----- Bán chạy nhất (bỏ đơn huỷ) -----
            model.TopProducts = await _context.OrderDetails
                .AsNoTracking()
                .Where(d => d.Order!.Status != OrderStatuses.Cancelled)
                .GroupBy(d => new { d.ProductVariant!.ProductId, d.ProductVariant.Product!.Name, d.ProductVariant.Product.Image })
                .Select(g => new DashboardTopProduct
                {
                    ProductId = g.Key.ProductId,
                    Name = g.Key.Name,
                    Image = g.Key.Image,
                    Sold = g.Sum(d => d.Quantity),
                    Revenue = g.Sum(d => d.Quantity * d.Price)
                })
                .OrderByDescending(x => x.Sold)
                .Take(5)
                .ToListAsync();

            // ----- Sắp hết / hết hàng (chỉ sản phẩm đang bán, ít hàng nhất lên đầu) -----
            model.LowStockProducts = await _context.Products
                .AsNoTracking()
                .Where(p => p.Status)
                .Select(p => new DashboardStockItem
                {
                    ProductId = p.Id,
                    Name = p.Name,
                    Image = p.Image,
                    Quantity = p.ProductVariants.Sum(v => v.Quantity)
                })
                .Where(x => x.Quantity <= LowStockThreshold)
                .OrderBy(x => x.Quantity)
                .ThenBy(x => x.Name)
                .Take(6)
                .ToListAsync();

            return View(model);
        }
    }
}
