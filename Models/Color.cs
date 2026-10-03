using System.ComponentModel.DataAnnotations;

namespace ShoeStore.Models
{
    public class Color
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên màu không được để trống")]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        public ICollection<ProductVariant> ProductVariants { get; set; }
            = new List<ProductVariant>();
    }
}