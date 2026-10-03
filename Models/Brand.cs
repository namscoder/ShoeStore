using System.ComponentModel.DataAnnotations;

namespace ShoeStore.Models
{
    public class Brand
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên thương hiệu không được để trống")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        // Một Brand có nhiều Product
        public ICollection<Product> Products { get; set; }
            = new List<Product>();
    }
}