using System.ComponentModel.DataAnnotations;

namespace ShoeStore.Models
{
    public class ProductImage
    {
        public int Id { get; set; }

        public int ProductId { get; set; }
        public Product? Product { get; set; }

        // null = ảnh chung của sản phẩm, có giá trị = ảnh riêng của màu đó
        public int? ColorId { get; set; }
        public Color? Color { get; set; }

        [Required]
        [StringLength(255)]
        public string ImageUrl { get; set; } = string.Empty;

        // Số nhỏ hiện trước
        public int SortOrder { get; set; }
    }
}