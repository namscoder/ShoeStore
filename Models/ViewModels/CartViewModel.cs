namespace ShoeStore.Models.ViewModels
{
    // Dữ liệu trang giỏ hàng (không phải bảng trong database)
    public class CartViewModel
    {
        public List<CartLineViewModel> Lines { get; set; } = new();

        // Chỉ tính các dòng còn mua được (sản phẩm đang bán, còn hàng)
        public int TotalQuantity => Lines.Where(l => l.CanBuy).Sum(l => l.Quantity);

        public decimal Subtotal => Lines.Where(l => l.CanBuy).Sum(l => l.LineTotal);

        // Có dòng nào hết hàng / ngừng bán / vượt tồn kho không
        public bool HasProblems => Lines.Any(l => !l.CanBuy || l.Quantity > l.Stock);
    }

    // Một dòng trong giỏ: 1 biến thể (size + màu) của 1 sản phẩm
    public class CartLineViewModel
    {
        public int CartItemId { get; set; }

        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? BrandName { get; set; }
        public string? Image { get; set; }

        public string? ColorName { get; set; }
        public string? SizeName { get; set; }

        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }

        // Số đôi còn trong kho của biến thể này
        public int Stock { get; set; }

        // Sản phẩm còn đang bán (không bị Ẩn)
        public bool ProductActive { get; set; }

        public bool CanBuy => ProductActive && Stock > 0;

        public decimal LineTotal => UnitPrice * Quantity;
    }
}
