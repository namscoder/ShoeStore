using System.Globalization;

namespace ShoeStore.Models
{
    // Dữ liệu hiển thị trên trang chi tiết sản phẩm (không phải bảng trong database)
    public class ProductDetailViewModel
    {
        public Product Product { get; set; } = null!;

        // Tổng số đôi đã bán (không tính đơn đã huỷ)
        public int SoldQuantity { get; set; }

        // Sản phẩm cùng danh mục / cùng thương hiệu
        public List<Product> RelatedProducts { get; set; } = new();

        // Tổng tồn kho của mọi biến thể
        public int TotalStock => Product.ProductVariants.Sum(v => v.Quantity);

        // Các màu của sản phẩm (không trùng)
        public List<Color> Colors => Product.ProductVariants
            .Where(v => v.Color != null)
            .Select(v => v.Color!)
            .DistinctBy(c => c.Id)
            .OrderBy(c => c.Name)
            .ToList();

        // Các size của sản phẩm: số trước (38, 39...), chữ sau (S, M...)
        public List<Size> Sizes => Product.ProductVariants
            .Where(v => v.Size != null)
            .Select(v => v.Size!)
            .DistinctBy(s => s.Id)
            .OrderBy(s => decimal.TryParse(s.Name, NumberStyles.Any, CultureInfo.InvariantCulture, out _) ? 0 : 1)
            .ThenBy(s => decimal.TryParse(s.Name, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : 0)
            .ThenBy(s => s.Name)
            .ToList();
    }
}
