namespace ShoeStore.Models
{
    // Dữ liệu hiển thị trên trang thương hiệu (không phải bảng trong database)
    public class BrandPageViewModel
    {
        public Brand Brand { get; set; } = null!;

        // Sản phẩm đang bán của thương hiệu này
        public List<Product> Products { get; set; } = new();

        // Các thương hiệu khác để chuyển nhanh
        public List<HomeGroupItem> OtherBrands { get; set; } = new();
    }
}
