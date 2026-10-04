using Microsoft.EntityFrameworkCore;
using ShoeStore.Data;
using ShoeStore.Models.ViewModels;

namespace ShoeStore.Services
{
    // Đọc giỏ hàng của 1 người dùng thành dữ liệu hiển thị.
    // Dùng chung cho trang Giỏ hàng và trang Đặt hàng để 2 nơi luôn tính giống nhau.
    public class CartService
    {
        private readonly ApplicationDbContext _context;

        public CartService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<CartViewModel> GetCartAsync(int userId)
        {
            var items = await _context.CartItems
                .AsNoTracking()
                .Where(c => c.UserId == userId)
                .Include(c => c.ProductVariant!).ThenInclude(v => v.Product!).ThenInclude(p => p.Brand)
                .Include(c => c.ProductVariant!).ThenInclude(v => v.Product!).ThenInclude(p => p.Images)
                .Include(c => c.ProductVariant!).ThenInclude(v => v.Size)
                .Include(c => c.ProductVariant!).ThenInclude(v => v.Color)
                .AsSplitQuery()
                .OrderBy(c => c.Id)
                .ToListAsync();

            return new CartViewModel
            {
                Lines = items.Select(c =>
                {
                    var variant = c.ProductVariant!;
                    var product = variant.Product!;

                    // Ưu tiên ảnh đúng màu đã chọn, không có thì dùng ảnh đại diện
                    var colorImage = product.Images
                        .Where(i => i.ColorId == variant.ColorId)
                        .OrderBy(i => i.SortOrder)
                        .Select(i => i.ImageUrl)
                        .FirstOrDefault();

                    return new CartLineViewModel
                    {
                        CartItemId = c.Id,
                        ProductId = product.Id,
                        ProductName = product.Name,
                        BrandName = product.Brand?.Name,
                        Image = colorImage ?? product.Image,
                        ColorName = variant.Color?.Name,
                        SizeName = variant.Size?.Name,
                        UnitPrice = product.Price,
                        Quantity = c.Quantity,
                        Stock = variant.Quantity,
                        ProductActive = product.Status
                    };
                }).ToList()
            };
        }
    }
}
