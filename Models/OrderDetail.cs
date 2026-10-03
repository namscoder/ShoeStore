using System.ComponentModel.DataAnnotations;

namespace ShoeStore.Models
{
    public class OrderDetail
    {
        public int Id { get; set; }

        public int OrderId { get; set; }

        public Order? Order { get; set; }

        public int ProductVariantId { get; set; }

        public ProductVariant? ProductVariant { get; set; }

        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        [Range(0, double.MaxValue)]
        public decimal Price { get; set; }
    }
}