using System.Security.Claims;
using System.Text.Json;
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
    // Phía khách hàng: đặt hàng, xem đơn hàng của mình, huỷ đơn
    [Authorize]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly CartService _cartService;
        private readonly OrderService _orderService;

        public OrderController(ApplicationDbContext context, CartService cartService, OrderService orderService)
        {
            _context = context;
            _cartService = cartService;
            _orderService = orderService;
        }

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // ===================== Trang đặt hàng =====================

        [HttpGet("dat-hang")]
        public async Task<IActionResult> Checkout()
        {
            var cart = await _cartService.GetCartAsync(CurrentUserId);

            // Giỏ trống hoặc có sản phẩm lỗi thì quay về giỏ để xử lý trước
            var error = CheckCart(cart);
            if (error != null)
            {
                TempData["ErrorMessage"] = error;
                return RedirectToAction("Index", "Cart");
            }

            var user = await _context.Users.AsNoTracking().FirstAsync(u => u.Id == CurrentUserId);

            // Lần trước bị lỗi thì lấy lại dữ liệu đã nhập, không thì điền sẵn từ tài khoản
            var model = RestoreFormState() ?? FromProfile(user);
            model.Cart = cart;
            model.ReceiverName = user.FullName ?? user.Username;

            return View(model);
        }

        [HttpPost("dat-hang")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(CheckoutViewModel model)
        {
            var userId = CurrentUserId;

            model.Phone = model.Phone?.Trim() ?? string.Empty;

            // Bỏ qua kiểm tra các phần chỉ để hiển thị
            ModelState.Remove(nameof(model.Cart));
            ModelState.Remove(nameof(model.ReceiverName));
            ModelState.Remove(nameof(model.SavedAddressHint));

            // Kiểm tra địa chỉ và ghép thành 1 chuỗi để lưu vào đơn hàng
            BuildShippingAddress(model);

            if (!ModelState.IsValid)
            {
                SaveFormState(model);
                return RedirectToAction(nameof(Checkout));
            }

            // Mọi bước dưới đây chạy trong 1 giao dịch: lỗi ở đâu thì huỷ hết, không trừ kho "một nửa"
            await using var transaction = await _context.Database.BeginTransactionAsync();

            var items = await _context.CartItems
                .Where(c => c.UserId == userId)
                .Include(c => c.ProductVariant!).ThenInclude(v => v.Product)
                .Include(c => c.ProductVariant!).ThenInclude(v => v.Size)
                .Include(c => c.ProductVariant!).ThenInclude(v => v.Color)
                .ToListAsync();

            if (items.Count == 0)
            {
                TempData["ErrorMessage"] = "Giỏ hàng đang trống";
                return RedirectToAction("Index", "Cart");
            }

            var order = new Order
            {
                UserId = userId,
                OrderDate = DateTime.Now,
                Status = OrderStatuses.Pending,
                Phone = model.Phone,
                ShippingAddress = model.ShippingAddress!,
                TotalAmount = 0
            };

            foreach (var item in items)
            {
                var variant = item.ProductVariant!;
                var product = variant.Product!;
                var label = $"\"{product.Name}\" (màu {variant.Color?.Name}, size {variant.Size?.Name})";

                if (!product.Status)
                {
                    return CancelCheckout($"{label} đã ngừng bán, hãy xoá khỏi giỏ hàng");
                }

                // Trừ kho NGAY TRONG CÂU SQL, kèm điều kiện "còn đủ hàng".
                // Nếu 2 người cùng mua đôi cuối cùng, chỉ 1 người trừ được (người kia nhận 0 dòng bị sửa).
                var updated = await _context.ProductVariants
                    .Where(v => v.Id == variant.Id && v.Quantity >= item.Quantity)
                    .ExecuteUpdateAsync(s => s.SetProperty(v => v.Quantity, v => v.Quantity - item.Quantity));

                if (updated == 0)
                {
                    return CancelCheckout($"{label} không còn đủ {item.Quantity} đôi trong kho");
                }

                // Lưu giá TẠI THỜI ĐIỂM MUA: sau này đổi giá sản phẩm thì đơn cũ vẫn đúng
                order.OrderDetails.Add(new OrderDetail
                {
                    ProductVariantId = variant.Id,
                    Quantity = item.Quantity,
                    Price = product.Price
                });
                order.TotalAmount += product.Price * item.Quantity;
            }

            _context.Orders.Add(order);
            _context.CartItems.RemoveRange(items);

            if (model.SaveToProfile)
            {
                var user = await _context.Users.FirstAsync(u => u.Id == userId);
                user.Phone = model.Phone;
                user.Address = model.ShippingAddress;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["SuccessMessage"] = "Đặt hàng thành công! Cửa hàng sẽ sớm liên hệ để xác nhận đơn.";
            return RedirectToAction(nameof(Detail), new { id = order.Id });
        }

        // ===================== Đơn hàng của tôi =====================

        [HttpGet("don-hang")]
        public async Task<IActionResult> Index()
        {
            var orders = await _context.Orders
                .AsNoTracking()
                .Where(o => o.UserId == CurrentUserId)
                .Include(o => o.OrderDetails).ThenInclude(d => d.ProductVariant!).ThenInclude(v => v.Product!).ThenInclude(p => p.Images)
                .Include(o => o.OrderDetails).ThenInclude(d => d.ProductVariant!).ThenInclude(v => v.Size)
                .Include(o => o.OrderDetails).ThenInclude(d => d.ProductVariant!).ThenInclude(v => v.Color)
                .AsSplitQuery()
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(orders);
        }

        [HttpGet("don-hang/{id:int}")]
        public async Task<IActionResult> Detail(int id)
        {
            // Chỉ xem được đơn CỦA MÌNH
            var order = await _context.Orders
                .AsNoTracking()
                .Include(o => o.OrderDetails).ThenInclude(d => d.ProductVariant!).ThenInclude(v => v.Product!).ThenInclude(p => p.Images)
                .Include(o => o.OrderDetails).ThenInclude(d => d.ProductVariant!).ThenInclude(v => v.Product!).ThenInclude(p => p.Brand)
                .Include(o => o.OrderDetails).ThenInclude(d => d.ProductVariant!).ThenInclude(v => v.Size)
                .Include(o => o.OrderDetails).ThenInclude(d => d.ProductVariant!).ThenInclude(v => v.Color)
                .AsSplitQuery()
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == CurrentUserId);

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        // Khách chỉ huỷ được đơn còn "Chờ xác nhận". Huỷ thì trả lại hàng vào kho.
        [HttpPost("don-hang/{id:int}/huy")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            // Chỉ huỷ được đơn CỦA MÌNH
            var isMine = await _context.Orders.AnyAsync(o => o.Id == id && o.UserId == CurrentUserId);
            if (!isMine)
            {
                return NotFound();
            }

            // Chỉ thành công nếu đơn VẪN đang chờ xác nhận (cửa hàng chưa kịp xác nhận)
            var cancelled = await _orderService.ChangeStatusAsync(id, OrderStatuses.Pending, OrderStatuses.Cancelled);

            if (cancelled)
            {
                TempData["SuccessMessage"] = $"Đã huỷ đơn hàng #{id}";
            }
            else
            {
                TempData["ErrorMessage"] = "Chỉ huỷ được đơn hàng đang chờ xác nhận. Vui lòng liên hệ cửa hàng.";
            }

            return RedirectToAction(nameof(Detail), new { id });
        }

        // Đặt lại đơn đã huỷ: cho lại các sản phẩm của đơn vào giỏ hàng (theo tồn kho HIỆN TẠI)
        [HttpPost("don-hang/{id:int}/dat-lai")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reorder(int id)
        {
            var userId = CurrentUserId;

            var order = await _context.Orders
                .AsNoTracking()
                .Include(o => o.OrderDetails).ThenInclude(d => d.ProductVariant!).ThenInclude(v => v.Product)
                .Include(o => o.OrderDetails).ThenInclude(d => d.ProductVariant!).ThenInclude(v => v.Size)
                .Include(o => o.OrderDetails).ThenInclude(d => d.ProductVariant!).ThenInclude(v => v.Color)
                .AsSplitQuery()
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null)
            {
                return NotFound();
            }

            if (order.Status != OrderStatuses.Cancelled)
            {
                TempData["ErrorMessage"] = "Chỉ đặt lại được đơn hàng đã huỷ";
                return RedirectToAction(nameof(Detail), new { id });
            }

            var cartItems = await _context.CartItems.Where(c => c.UserId == userId).ToListAsync();
            var addedPairs = 0;
            var problems = new List<string>();

            foreach (var detail in order.OrderDetails)
            {
                var variant = detail.ProductVariant!;
                var product = variant.Product!;
                var label = $"{product.Name} ({variant.Color?.Name}, size {variant.Size?.Name})";

                if (!product.Status)
                {
                    problems.Add($"{label} đã ngừng bán");
                    continue;
                }

                // Giỏ đã có sẵn biến thể này thì cộng dồn, nhưng không vượt tồn kho
                var item = cartItems.FirstOrDefault(c => c.ProductVariantId == variant.Id);
                var canAdd = variant.Quantity - (item?.Quantity ?? 0);

                if (canAdd <= 0)
                {
                    problems.Add($"{label} đã hết hàng");
                    continue;
                }

                var quantity = Math.Min(detail.Quantity, canAdd);
                if (quantity < detail.Quantity)
                {
                    problems.Add($"{label} chỉ còn đủ {quantity}/{detail.Quantity} đôi");
                }

                if (item == null)
                {
                    item = new CartItem { UserId = userId, ProductVariantId = variant.Id, Quantity = 0 };
                    _context.CartItems.Add(item);
                    cartItems.Add(item);
                }

                item.Quantity += quantity;
                addedPairs += quantity;
            }

            await _context.SaveChangesAsync();

            if (addedPairs > 0)
            {
                TempData["SuccessMessage"] = $"Đã thêm {addedPairs} đôi từ đơn #{order.Id} vào giỏ hàng. Kiểm tra lại rồi bấm Tiến hành đặt hàng nhé.";
            }

            if (problems.Count > 0)
            {
                TempData["ErrorMessage"] = (addedPairs > 0 ? "Lưu ý: " : "Không thể đặt lại: ") + string.Join("; ", problems) + ".";
            }

            return addedPairs > 0
                ? RedirectToAction("Index", "Cart")
                : RedirectToAction(nameof(Detail), new { id });
        }

        // ===================== Hàm hỗ trợ =====================

        // Kiểm tra địa chỉ rồi ghép thành chuỗi đầy đủ vào model.ShippingAddress
        // vd: "12 Nguyễn Huệ, Phường Sài Gòn, Thành phố Hồ Chí Minh"
        private void BuildShippingAddress(CheckoutViewModel model)
        {
            if (model.AddressMode == "manual")
            {
                // Dự phòng: khách tự nhập cả địa chỉ
                model.ShippingAddress = model.ShippingAddress?.Trim();

                if (string.IsNullOrEmpty(model.ShippingAddress) || model.ShippingAddress.Length < 10)
                {
                    ModelState.AddModelError(nameof(model.ShippingAddress), "Vui lòng nhập địa chỉ giao hàng đầy đủ (ít nhất 10 ký tự)");
                }
                return;
            }

            model.AddressMode = "picker";
            model.ProvinceName = model.ProvinceName?.Trim();
            model.WardName = model.WardName?.Trim();
            model.Street = model.Street?.Trim();

            if (string.IsNullOrEmpty(model.ProvinceName))
            {
                ModelState.AddModelError(nameof(model.ProvinceName), "Vui lòng chọn tỉnh/thành phố");
            }

            if (string.IsNullOrEmpty(model.WardName))
            {
                ModelState.AddModelError(nameof(model.WardName), "Vui lòng chọn phường/xã");
            }

            if (string.IsNullOrEmpty(model.Street) || model.Street.Length < 3)
            {
                ModelState.AddModelError(nameof(model.Street), "Vui lòng nhập số nhà, tên đường");
            }

            model.ShippingAddress = AddressFormatter.Join(model.Street ?? "", model.WardName ?? "", model.ProvinceName ?? "");

            if (model.ShippingAddress.Length > 255)
            {
                ModelState.AddModelError(nameof(model.Street), "Địa chỉ quá dài, hãy rút gọn số nhà, tên đường");
            }
        }

        // Điền sẵn form từ tài khoản. Địa chỉ đã lưu dạng "số nhà, Phường/Xã ..., Tỉnh/Thành phố ..."
        // thì tách ngược lại để chọn sẵn tỉnh, phường; không tách được thì hiện để khách tham khảo.
        private static CheckoutViewModel FromProfile(User user)
        {
            var model = new CheckoutViewModel { Phone = user.Phone ?? string.Empty };

            if (AddressFormatter.TrySplit(user.Address, out var province, out var ward, out var street))
            {
                model.ProvinceName = province;
                model.WardName = ward;
                model.Street = street;
            }
            else if (!string.IsNullOrWhiteSpace(user.Address))
            {
                model.SavedAddressHint = user.Address;
            }

            return model;
        }

        // Giỏ có đặt được không: trống / có sản phẩm ngừng bán, hết hàng, vượt tồn kho => trả về câu báo lỗi
        private static string? CheckCart(CartViewModel cart)
        {
            if (!cart.Lines.Any())
            {
                return "Giỏ hàng đang trống";
            }

            if (cart.HasProblems)
            {
                return "Giỏ hàng có sản phẩm đã hết hàng, ngừng bán hoặc vượt số lượng trong kho. Hãy xử lý trước khi đặt hàng.";
            }

            return null;
        }

        // Dừng đặt hàng: chưa Commit nên giao dịch tự huỷ, kho không bị trừ
        private IActionResult CancelCheckout(string message)
        {
            TempData["ErrorMessage"] = $"Không thể đặt hàng: {message}";
            return RedirectToAction("Index", "Cart");
        }

        // Post-Redirect-Get: lưu tạm dữ liệu đã nhập và lỗi vào TempData
        private void SaveFormState(CheckoutViewModel model)
        {
            TempData["CheckoutForm"] = JsonSerializer.Serialize(new
            {
                model.Phone,
                model.ProvinceName,
                model.WardName,
                model.Street,
                model.AddressMode,
                // Chế độ nhập tay thì giữ lại cả địa chỉ đã gõ
                ShippingAddress = model.AddressMode == "manual" ? model.ShippingAddress : null,
                model.SaveToProfile
            });
            TempData["CheckoutErrors"] = JsonSerializer.Serialize(
                ModelState
                    .Where(x => x.Value!.Errors.Count > 0)
                    .ToDictionary(
                        x => x.Key,
                        x => x.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                    )
            );
        }

        private CheckoutViewModel? RestoreFormState()
        {
            CheckoutViewModel? model = null;

            if (TempData["CheckoutForm"] is string formJson)
            {
                model = JsonSerializer.Deserialize<CheckoutViewModel>(formJson);
            }

            if (TempData["CheckoutErrors"] is string errorsJson)
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

            return model;
        }
    }
}
