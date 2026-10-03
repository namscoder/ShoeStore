using System.ComponentModel.DataAnnotations;

namespace ShoeStore.Models
{
    public class Order
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public User? User { get; set; }

        public DateTime OrderDate { get; set; } = DateTime.Now;

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Pending";

        [Required]
        [StringLength(255)]
        public string ShippingAddress { get; set; } = string.Empty;

        [StringLength(20)]
        public string? Phone { get; set; }

        [Range(0, double.MaxValue)]
        public decimal TotalAmount { get; set; }

        public ICollection<OrderDetail> OrderDetails { get; set; }
            = new List<OrderDetail>();
    }
}