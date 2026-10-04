using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShoeStore.Data;
using ShoeStore.Models;
using ShoeStore.Models.ViewModels;
using ShoeStore.Services;

namespace ShoeStore.Controllers
{
    // Quản lý đơn hàng (admin): xem, lọc, chuyển trạng thái theo quy trình
    [Authorize(Roles = AppRoles.Admin)]
    [Route("Admin/Orders")]
    public class AdminOrdersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly OrderService _orderService;

        private const int PageSize = 15;

        public AdminOrdersController(ApplicationDbContext context, OrderService orderService)
        {
            _context = context;
            _orderService = orderService;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index([FromQuery] OrderFilter filter)
        {
            var query = _context.Orders
                .AsNoTracking()
                .Include(o => o.User)
                .Include(o => o.OrderDetails)
                .AsQueryable();

            // Tìm theo mã đơn ("12" hoặc "#12") hoặc thông tin khách
            if (!string.IsNullOrWhiteSpace(filter.Keyword))
            {
                var keyword = filter.Keyword.Trim().TrimStart('#');

                if (int.TryParse(keyword, out var orderId))
                {
                    query = query.Where(o => o.Id == orderId || (o.Phone != null && o.Phone.Contains(keyword)));
                }
                else
                {
                    query = query.Where(o =>
                        (o.Phone != null && o.Phone.Contains(keyword)) ||
                        o.User!.Username.Contains(keyword) ||
                        o.User.Email.Contains(keyword) ||
                        (o.User.FullName != null && o.User.FullName.Contains(keyword)));
                }
            }

            if (filter.FromDate.HasValue)
            {
                var from = filter.FromDate.Value.Date;
                query = query.Where(o => o.OrderDate >= from);
            }

            if (filter.ToDate.HasValue)
            {
                var toExclusive = filter.ToDate.Value.Date.AddDays(1); // hết ngày ToDate
                query = query.Where(o => o.OrderDate < toExclusive);
            }

            // Số đơn mỗi trạng thái (theo tìm kiếm / ngày đang lọc) để hiện trên các tab
            var statusCounts = await query
                .GroupBy(o => o.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Status, x => x.Count);

            if (!string.IsNullOrEmpty(filter.Status) && OrderStatuses.All.Contains(filter.Status))
            {
                query = query.Where(o => o.Status == filter.Status);
            }
            else
            {
                filter.Status = null;
            }

            var totalItems = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
            filter.Page = Math.Clamp(filter.Page, 1, totalPages);

            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .ThenByDescending(o => o.Id)
                .Skip((filter.Page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            ViewBag.Filter = filter;
            ViewBag.StatusCounts = statusCounts;
            ViewBag.TotalItems = totalItems;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = PageSize;

            return View("~/Views/Admin/Orders/Index.cshtml", orders);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Detail(int id)
        {
            var order = await _context.Orders
                .AsNoTracking()
                .Include(o => o.User)
                .Include(o => o.OrderDetails).ThenInclude(d => d.ProductVariant!).ThenInclude(v => v.Product!).ThenInclude(p => p.Images)
                .Include(o => o.OrderDetails).ThenInclude(d => d.ProductVariant!).ThenInclude(v => v.Product!).ThenInclude(p => p.Brand)
                .Include(o => o.OrderDetails).ThenInclude(d => d.ProductVariant!).ThenInclude(v => v.Size)
                .Include(o => o.OrderDetails).ThenInclude(d => d.ProductVariant!).ThenInclude(v => v.Color)
                .AsSplitQuery()
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            // Số đơn khách này đã đặt (để admin biết khách quen hay khách mới)
            ViewBag.CustomerOrderCount = await _context.Orders.CountAsync(o => o.UserId == order.UserId);

            return View("~/Views/Admin/Orders/Detail.cshtml", order);
        }

        // Chuyển trạng thái đơn. currentStatus = trạng thái admin đang nhìn thấy khi bấm nút.
        [HttpPost("{id:int}/status")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string currentStatus, string newStatus, string? returnUrl)
        {
            var exists = await _context.Orders.AnyAsync(o => o.Id == id);
            if (!exists)
            {
                return NotFound();
            }

            // Chỉ cho chuyển đúng quy trình, vd không cho nhảy từ "Chờ xác nhận" sang "Hoàn thành"
            if (!OrderStatuses.NextStatuses(currentStatus).Contains(newStatus))
            {
                TempData["ErrorMessage"] =
                    $"Không thể chuyển đơn #{id} từ \"{OrderStatuses.DisplayName(currentStatus)}\" " +
                    $"sang \"{OrderStatuses.DisplayName(newStatus)}\"";
                return RedirectBack(returnUrl, id);
            }

            var changed = await _orderService.ChangeStatusAsync(id, currentStatus, newStatus);

            if (changed)
            {
                TempData["SuccessMessage"] =
                    $"Đơn #{id}: {OrderStatuses.DisplayName(currentStatus)} → {OrderStatuses.DisplayName(newStatus)}" +
                    (newStatus == OrderStatuses.Cancelled ? " (đã trả hàng về kho)" : "");
            }
            else
            {
                // Trạng thái đã bị đổi trước đó (admin khác vừa xử lý, hoặc khách vừa tự huỷ)
                TempData["ErrorMessage"] =
                    $"Đơn #{id} vừa được cập nhật bởi người khác, không còn ở trạng thái " +
                    $"\"{OrderStatuses.DisplayName(currentStatus)}\". Vui lòng xem lại.";
            }

            return RedirectBack(returnUrl, id);
        }

        // Quay lại trang đang xem (danh sách kèm bộ lọc, hoặc trang chi tiết). Chỉ chấp nhận đường dẫn nội bộ.
        private IActionResult RedirectBack(string? returnUrl, int id)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction(nameof(Detail), new { id });
        }
    }
}
