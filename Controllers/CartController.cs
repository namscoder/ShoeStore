using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShoeStore.Data;
using ShoeStore.Models;
using ShoeStore.Services;

namespace ShoeStore.Controllers
{
    // Giỏ hàng của khách: lưu trong bảng CartItems theo tài khoản => cần đăng nhập
    [Route("gio-hang")]
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly CartService _cartService;

        public CartController(ApplicationDbContext context, CartService cartService)
        {
            _context = context;
            _cartService = cartService;
        }

        // Id người đang đăng nhập, chưa đăng nhập thì null
        private int? CurrentUserId =>
            User.Identity?.IsAuthenticated == true &&
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
                ? id
                : null;

        // Gọi bằng JavaScript (fetch) thì trả JSON, bấm nút bình thường thì chuyển trang
        private bool IsAjax => Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        // ===================== Trang giỏ hàng =====================

        [Authorize]
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var model = await _cartService.GetCartAsync(CurrentUserId!.Value);
            return View(model);
        }

        // ===================== Thêm vào giỏ (từ trang chi tiết) =====================

        [HttpPost("them")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(int productVariantId, int quantity, string? returnUrl)
        {
            returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Action("Index", "Home");

            // Chưa đăng nhập => đi đăng nhập, xong quay lại đúng trang sản phẩm
            if (CurrentUserId is not int userId)
            {
                var loginUrl = Url.Action("Login", "Account", new { returnUrl })!;
                return IsAjax
                    ? Json(new { success = false, requireLogin = true, loginUrl })
                    : Redirect(loginUrl);
            }

            quantity = Math.Max(1, quantity);

            var variant = await _context.ProductVariants
                .Include(v => v.Product)
                .FirstOrDefaultAsync(v => v.Id == productVariantId);

            if (variant?.Product == null || !variant.Product.Status)
            {
                return AddResult(false, "Sản phẩm không tồn tại hoặc đã ngừng bán", returnUrl!);
            }

            // Cùng biến thể đã có trong giỏ thì cộng dồn số lượng (không tạo dòng mới)
            var item = await _context.CartItems
                .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductVariantId == productVariantId);

            var inCart = item?.Quantity ?? 0;
            var canAdd = variant.Quantity - inCart;

            if (canAdd <= 0)
            {
                var message = inCart > 0
                    ? $"Giỏ hàng đã có {inCart} đôi, bằng số lượng còn lại trong kho"
                    : "Màu và size này đã hết hàng";
                return AddResult(false, message, returnUrl!);
            }

            var added = Math.Min(quantity, canAdd);

            if (item == null)
            {
                _context.CartItems.Add(new CartItem
                {
                    UserId = userId,
                    ProductVariantId = productVariantId,
                    Quantity = added
                });
            }
            else
            {
                item.Quantity += added;
            }

            await _context.SaveChangesAsync();

            var successMessage = added < quantity
                ? $"Đã thêm {added} đôi vào giỏ (kho chỉ còn {variant.Quantity} đôi)"
                : $"Đã thêm {added} đôi vào giỏ hàng";

            return AddResult(true, successMessage, returnUrl!, await CartCountAsync(userId));
        }

        // ===================== Sửa số lượng / Xoá =====================

        [Authorize]
        [HttpPost("cap-nhat/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(int id, int quantity)
        {
            var userId = CurrentUserId!.Value;

            // Chỉ sửa được dòng trong giỏ CỦA MÌNH
            var item = await _context.CartItems
                .Include(c => c.ProductVariant)
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

            if (item == null)
            {
                return RedirectToAction(nameof(Index));
            }

            var stock = item.ProductVariant!.Quantity;

            if (quantity > stock)
            {
                quantity = stock;
                TempData["ErrorMessage"] = stock > 0
                    ? $"Kho chỉ còn {stock} đôi cho size và màu này"
                    : "Size và màu này đã hết hàng nên đã được xoá khỏi giỏ";
            }

            if (quantity <= 0)
            {
                _context.CartItems.Remove(item);
            }
            else
            {
                item.Quantity = quantity;
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [Authorize]
        [HttpPost("xoa/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int id)
        {
            var userId = CurrentUserId!.Value;

            var item = await _context.CartItems
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

            if (item != null)
            {
                _context.CartItems.Remove(item);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã xoá sản phẩm khỏi giỏ hàng";
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize]
        [HttpPost("xoa-tat-ca")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Clear()
        {
            var userId = CurrentUserId!.Value;

            var items = await _context.CartItems.Where(c => c.UserId == userId).ToListAsync();
            _context.CartItems.RemoveRange(items);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã xoá toàn bộ giỏ hàng";
            return RedirectToAction(nameof(Index));
        }

        // ===================== Hàm hỗ trợ =====================

        // Tổng số đôi trong giỏ (hiện trên biểu tượng giỏ hàng ở header)
        private Task<int> CartCountAsync(int userId)
        {
            return _context.CartItems
                .Where(c => c.UserId == userId)
                .SumAsync(c => c.Quantity);
        }

        // Kết quả "thêm vào giỏ": JSON cho JavaScript, hoặc thông báo + quay lại trang sản phẩm
        private IActionResult AddResult(bool success, string message, string returnUrl, int? cartCount = null)
        {
            if (IsAjax)
            {
                return Json(new { success, message, cartCount, cartUrl = Url.Action(nameof(Index)) });
            }

            TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
            return LocalRedirect(returnUrl);
        }
    }
}
