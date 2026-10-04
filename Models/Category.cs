using System.ComponentModel.DataAnnotations;

namespace ShoeStore.Models
{
    public class Category
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên danh mục không được để trống")]
        [StringLength(100, ErrorMessage = "Tên danh mục không được vượt quá 100 ký tự")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        // Một Category có nhiều Product
        public ICollection<Product> Products { get; set; }
            = new List<Product>();
    }
}