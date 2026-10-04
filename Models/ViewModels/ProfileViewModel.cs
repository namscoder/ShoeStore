using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ShoeStore.Models.ViewModels
{
    // Form "Thông tin tài khoản" (không phải bảng trong database).
    // Không có Username, Role, Status... nên người dùng không thể tự sửa mấy thứ đó.
    public class ProfileViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        [StringLength(100, ErrorMessage = "Họ tên không được vượt quá 100 ký tự")]
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        [StringLength(100)]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [RegularExpression(@"^(0|\+84)\d{9}$", ErrorMessage = "Số điện thoại không hợp lệ (vd: 0912345678)")]
        [Display(Name = "Số điện thoại")]
        public string? Phone { get; set; }

        // ===== Địa chỉ mặc định (không bắt buộc), chọn giống trang Đặt hàng =====
        [StringLength(100)]
        public string? ProvinceName { get; set; }

        [StringLength(100)]
        public string? WardName { get; set; }

        [StringLength(150, ErrorMessage = "Số nhà, tên đường không được vượt quá 150 ký tự")]
        public string? Street { get; set; }

        // Dự phòng khi không tải được danh sách tỉnh/thành: nhập tay cả địa chỉ
        [StringLength(255, ErrorMessage = "Địa chỉ không được vượt quá 255 ký tự")]
        public string? Address { get; set; }

        // "picker" = chọn từ danh sách, "manual" = nhập tay
        public string AddressMode { get; set; } = "picker";

        // ===== Chỉ để hiển thị (không nhận từ form, không kiểm tra) =====
        [BindNever, ValidateNever]
        public ProfileSummary? Summary { get; set; }

        // Địa chỉ đã lưu nhưng không tách được thành tỉnh/phường (vd: nhập tay lúc đặt hàng)
        [BindNever, ValidateNever]
        public string? SavedAddressHint { get; set; }
    }

    // Thông tin tổng quan hiện trên trang tài khoản
    public class ProfileSummary
    {
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        public int TotalOrders { get; set; }
        public int ProcessingOrders { get; set; }   // Chờ xác nhận + Đã xác nhận + Đang giao
        public int CompletedOrders { get; set; }
        public int CancelledOrders { get; set; }
        public decimal TotalSpent { get; set; }     // Chỉ tính đơn đã hoàn thành
    }

    // Form "Đổi mật khẩu"
    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu hiện tại")]
        public string CurrentPassword { get; set; } = string.Empty;

        // Cùng quy tắc với lúc đăng ký
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự")]
        [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$", ErrorMessage = "Mật khẩu phải có cả chữ và số")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu mới")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu mới")]
        [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu nhập lại không khớp")]
        [DataType(DataType.Password)]
        [Display(Name = "Nhập lại mật khẩu mới")]
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }
}
