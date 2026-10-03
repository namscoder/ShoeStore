using System.ComponentModel.DataAnnotations;

namespace ShoeStore.Models
{
    public class Size
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Kích thước không được để trống")]
        [StringLength(20)]
        public string Name { get; set; } = string.Empty;

        public ICollection<ProductVariant> ProductVariants { get; set; }
            = new List<ProductVariant>();
    }
}