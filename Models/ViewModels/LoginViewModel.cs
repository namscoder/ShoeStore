using System.ComponentModel.DataAnnotations;

namespace ShoeStore.Models.ViewModels
{
    // Dữ liệu form đăng nhập (không phải bảng trong database)
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập hoặc email")]
        [StringLength(100)]
        [Display(Name = "Tên đăng nhập hoặc email")]
        public string UsernameOrEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Ghi nhớ đăng nhập")]
        public bool RememberMe { get; set; }
    }
}
