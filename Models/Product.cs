using System.ComponentModel.DataAnnotations;

namespace ShoeStore.Models
{
    public class Product
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên sản phẩm không được để trống")]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Giá không được để trống")]
        [Range(1, double.MaxValue, ErrorMessage = "Giá phải lớn hơn 0")]        public decimal Price { get; set; }

        public string? Description { get; set; }

        public string? Image { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool Status { get; set; } = true;

        // Foreign Key
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn thương hiệu")]
        public int BrandId { get; set; }

        // Navigation Property
        public Brand? Brand { get; set; }

        // Foreign Key
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn danh mục")]
        public int CategoryId { get; set; }

        // Navigation Property
        public Category? Category { get; set; }

        // Một Product có nhiều ProductVariant
        public ICollection<ProductVariant> ProductVariants { get; set; }
            = new List<ProductVariant>();

        public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();    
    }
}