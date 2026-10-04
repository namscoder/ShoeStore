namespace ShoeStore.Models
{
    // Dữ liệu hiển thị trên trang chủ (không phải bảng trong database)
    public class HomeViewModel
    {
        // Sản phẩm mới nhất đang bán
        public List<Product> NewProducts { get; set; } = new();

        // Sản phẩm bán chạy nhất (đã sắp xếp theo số lượng bán)
        public List<BestSellerItem> BestSellers { get; set; } = new();

        public List<HomeGroupItem> Categories { get; set; } = new();

        public List<HomeGroupItem> Brands { get; set; } = new();

        public int TotalProducts { get; set; }
    }

    // Một danh mục / thương hiệu kèm số sản phẩm đang bán
    public class HomeGroupItem
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public int ProductCount { get; set; }
    }

    // Một sản phẩm bán chạy kèm tổng số lượng đã bán
    public class BestSellerItem
    {
        public Product Product { get; set; } = null!;

        public int SoldQuantity { get; set; }
    }
}
