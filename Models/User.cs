using System.ComponentModel.DataAnnotations;

namespace ShoeStore.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên đăng nhập không được để trống")]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email không được để trống")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [StringLength(100)]
        public string? FullName { get; set; }

        [StringLength(20)]
        public string? Phone { get; set; }

        [StringLength(255)]
        public string? Address { get; set; }

        public string Role { get; set; } = "Customer";

        public bool Status { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<Order> Orders { get; set; }
            = new List<Order>();

        public ICollection<CartItem> CartItems { get; set; }
            = new List<CartItem>();
    }
}