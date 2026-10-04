using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShoeStore.Data;
using ShoeStore.Infrastructure;
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

            query = ApplySearch(query, filter);

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

        // ===================== Xuất Excel =====================

        // GET /Admin/Orders/Export?Status=...&Keyword=...&FromDate=...&ToDate=...
        // Xuất ĐÚNG các đơn đang lọc trên trang danh sách (không phân trang), gồm 4 sheet:
        // Tổng quan | Đơn hàng | Chi tiết sản phẩm | Theo ngày
        [HttpGet("Export")]
        public async Task<IActionResult> Export([FromQuery] OrderFilter filter)
        {
            var query = ApplySearch(_context.Orders.AsNoTracking(), filter);

            if (!string.IsNullOrEmpty(filter.Status) && OrderStatuses.All.Contains(filter.Status))
            {
                query = query.Where(o => o.Status == filter.Status);
            }
            else
            {
                filter.Status = null;
            }

            var orders = await query
                .Include(o => o.User)
                .Include(o => o.OrderDetails).ThenInclude(d => d.ProductVariant!).ThenInclude(v => v.Product!).ThenInclude(p => p.Brand)
                .Include(o => o.OrderDetails).ThenInclude(d => d.ProductVariant!).ThenInclude(v => v.Size)
                .Include(o => o.OrderDetails).ThenInclude(d => d.ProductVariant!).ThenInclude(v => v.Color)
                .AsSplitQuery()
                .OrderByDescending(o => o.OrderDate)
                .ThenByDescending(o => o.Id)
                .ToListAsync();

            var book = new XlsxWorkbook();
            var completed = orders.Where(o => o.Status == OrderStatuses.Completed).ToList();
            var notCancelled = orders.Where(o => o.Status != OrderStatuses.Cancelled).ToList();

            // ----- Sheet 1: Tổng quan -----
            var summary = book.AddSheet("Tổng quan", new[] { 36d, 40d });
            summary.AddRow(XlsxCell.Text("BÁO CÁO ĐƠN HÀNG - SHOESTORE", XlsxStyle.Title));
            summary.AddEmptyRow();
            summary.AddRow("Thời gian xuất", XlsxCell.DateTime(DateTime.Now));
            summary.AddRow("Trạng thái", filter.Status == null ? "Tất cả" : OrderStatuses.DisplayName(filter.Status));
            summary.AddRow("Từ ngày", filter.FromDate.HasValue ? XlsxCell.Date(filter.FromDate.Value) : XlsxCell.Text("(không giới hạn)"));
            summary.AddRow("Đến ngày", filter.ToDate.HasValue ? XlsxCell.Date(filter.ToDate.Value) : XlsxCell.Text("(không giới hạn)"));
            summary.AddRow("Từ khoá tìm kiếm", string.IsNullOrWhiteSpace(filter.Keyword) ? "(không có)" : filter.Keyword.Trim());
            summary.AddEmptyRow();
            summary.AddRow(XlsxCell.Text("Tổng số đơn", XlsxStyle.Bold), XlsxCell.Number(orders.Count, XlsxStyle.BoldInteger));
            foreach (var status in OrderStatuses.All)
            {
                summary.AddRow("   " + OrderStatuses.DisplayName(status), XlsxCell.Number(orders.Count(o => o.Status == status)));
            }
            summary.AddRow(XlsxCell.Text("Giá trị đơn (không tính đơn huỷ)", XlsxStyle.Bold),
                XlsxCell.Money(notCancelled.Sum(o => o.TotalAmount), XlsxStyle.BoldMoney));
            summary.AddRow(XlsxCell.Text("Doanh thu (đơn hoàn thành)", XlsxStyle.Bold),
                XlsxCell.Money(completed.Sum(o => o.TotalAmount), XlsxStyle.BoldMoney));
            summary.AddRow(XlsxCell.Text("Số đôi đã bán (không tính đơn huỷ)", XlsxStyle.Bold),
                XlsxCell.Number(notCancelled.Sum(o => o.OrderDetails.Sum(d => d.Quantity)), XlsxStyle.BoldInteger));

            // ----- Sheet 2: Đơn hàng (mỗi đơn 1 dòng) -----
            var orderSheet = book.AddSheet("Đơn hàng", new[] { 9d, 17d, 24d, 16d, 28d, 14d, 45d, 9d, 15d, 15d });
            orderSheet.AddHeader("Mã đơn", "Ngày đặt", "Khách hàng", "Tên đăng nhập", "Email", "SĐT nhận", "Địa chỉ giao hàng", "Số đôi", "Tổng tiền", "Trạng thái");
            foreach (var o in orders)
            {
                orderSheet.AddRow(
                    XlsxCell.Number(o.Id),
                    XlsxCell.DateTime(o.OrderDate),
                    o.User?.FullName ?? o.User?.Username,
                    o.User?.Username,
                    o.User?.Email,
                    o.Phone,
                    o.ShippingAddress,
                    XlsxCell.Number(o.OrderDetails.Sum(d => d.Quantity)),
                    XlsxCell.Money(o.TotalAmount),
                    OrderStatuses.DisplayName(o.Status));
            }
            orderSheet.AddRow(
                XlsxCell.Text($"Tổng: {orders.Count} đơn", XlsxStyle.Bold),
                XlsxCell.Text("", XlsxStyle.Bold), XlsxCell.Text("", XlsxStyle.Bold), XlsxCell.Text("", XlsxStyle.Bold),
                XlsxCell.Text("", XlsxStyle.Bold), XlsxCell.Text("", XlsxStyle.Bold), XlsxCell.Text("", XlsxStyle.Bold),
                XlsxCell.Number(orders.Sum(o => o.OrderDetails.Sum(d => d.Quantity)), XlsxStyle.BoldInteger),
                XlsxCell.Money(orders.Sum(o => o.TotalAmount), XlsxStyle.BoldMoney),
                XlsxCell.Text("", XlsxStyle.Bold));

            // ----- Sheet 3: Chi tiết sản phẩm (mỗi sản phẩm trong đơn 1 dòng) -----
            var detailSheet = book.AddSheet("Chi tiết sản phẩm", new[] { 9d, 17d, 14d, 34d, 14d, 12d, 8d, 9d, 14d, 15d });
            detailSheet.AddHeader("Mã đơn", "Ngày đặt", "Trạng thái", "Sản phẩm", "Thương hiệu", "Màu", "Size", "Số lượng", "Đơn giá", "Thành tiền");
            foreach (var o in orders)
            {
                foreach (var d in o.OrderDetails)
                {
                    var variant = d.ProductVariant;
                    detailSheet.AddRow(
                        XlsxCell.Number(o.Id),
                        XlsxCell.DateTime(o.OrderDate),
                        OrderStatuses.DisplayName(o.Status),
                        variant?.Product?.Name,
                        variant?.Product?.Brand?.Name,
                        variant?.Color?.Name,
                        variant?.Size?.Name,
                        XlsxCell.Number(d.Quantity),
                        XlsxCell.Money(d.Price),
                        XlsxCell.Money(d.Price * d.Quantity));
                }
            }

            // ----- Sheet 4: Tổng hợp theo ngày -----
            var daySheet = book.AddSheet("Theo ngày", new[] { 13d, 10d, 13d, 10d, 22d, 22d });
            daySheet.AddHeader("Ngày", "Số đơn", "Hoàn thành", "Đã huỷ", "Giá trị đơn (trừ huỷ)", "Doanh thu (hoàn thành)");
            foreach (var day in orders.GroupBy(o => o.OrderDate.Date).OrderBy(g => g.Key))
            {
                daySheet.AddRow(
                    XlsxCell.Date(day.Key),
                    XlsxCell.Number(day.Count()),
                    XlsxCell.Number(day.Count(o => o.Status == OrderStatuses.Completed)),
                    XlsxCell.Number(day.Count(o => o.Status == OrderStatuses.Cancelled)),
                    XlsxCell.Money(day.Where(o => o.Status != OrderStatuses.Cancelled).Sum(o => o.TotalAmount)),
                    XlsxCell.Money(day.Where(o => o.Status == OrderStatuses.Completed).Sum(o => o.TotalAmount)));
            }

            var fileName = $"don-hang_{DateTime.Now:yyyyMMdd-HHmm}.xlsx";
            return File(book.ToBytes(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // ===================== Hàm hỗ trợ =====================

        // Tìm kiếm + lọc ngày (dùng chung cho trang danh sách và xuất Excel)
        private static IQueryable<Order> ApplySearch(IQueryable<Order> query, OrderFilter filter)
        {
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

            return query;
        }
    }
}
