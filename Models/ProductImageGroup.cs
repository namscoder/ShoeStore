namespace ShoeStore.Models
{
    // Một nhóm ảnh gửi lên từ form thêm/sửa sản phẩm.
    // ColorId = null => ảnh chung; có giá trị => ảnh riêng của màu đó.
    // Không phải bảng trong database, chỉ dùng để nhận dữ liệu từ form.
    public class ProductImageGroup
    {
        public int? ColorId { get; set; }

        public List<IFormFile> Files { get; set; } = new();
    }
}
