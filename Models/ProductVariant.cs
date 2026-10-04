using System.ComponentModel.DataAnnotations;

namespace ShoeStore.Models
{
    public class ProductVariant
    {
        public int Id { get; set; }

        public int ProductId { get; set; }

        public Product? Product { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn size")]
        public int SizeId { get; set; }

        public Size? Size { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn màu")] public int ColorId { get; set; }

        public Color? Color { get; set; }

        [Range(0, int.MaxValue,
            ErrorMessage = "Số lượng không được nhỏ hơn 0")]
        public int Quantity { get; set; }
    }
}