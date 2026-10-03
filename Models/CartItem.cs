using System.ComponentModel.DataAnnotations;

namespace ShoeStore.Models
{
    public class CartItem
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public User? User { get; set; }

        public int ProductVariantId { get; set; }

        public ProductVariant? ProductVariant { get; set; }

        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }
    }
}