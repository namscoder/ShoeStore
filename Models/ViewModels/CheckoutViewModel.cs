using System.ComponentModel.DataAnnotations;

namespace ShoeStore.Models.ViewModels
{
    // Dữ liệu trang Đặt hàng: thông tin giao hàng + giỏ hàng để hiển thị (không phải bảng trong database)
    public class CheckoutViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập số điện thoại nhận hàng")]
        [RegularExpression(@"^(0|\+84)\d{9}$", ErrorMessage = "Số điện thoại không hợp lệ (vd: 0912345678)")]
        [Display(Name = "Số điện thoại")]
        public string Phone { get; set; } = string.Empty;

        // ===== Cách 1 (mặc định): chọn như Shopee =====
        // Tỉnh/Thành phố -> Phường/Xã (đơn vị hành chính sau sáp nhập 07/2025) + số nhà, tên đường
        [StringLength(100)]
        public string? ProvinceName { get; set; }

        [StringLength(100)]
        public string? WardName { get; set; }

        [StringLength(150, ErrorMessage = "Số nhà, tên đường không được vượt quá 150 ký tự")]
        [Display(Name = "Số nhà, tên đường")]
        public string? Street { get; set; }

        // ===== Cách 2 (dự phòng): không tải được danh sách tỉnh/thành => nhập tay cả địa chỉ =====
        [StringLength(255, ErrorMessage = "Địa chỉ không được vượt quá 255 ký tự")]
        [Display(Name = "Địa chỉ giao hàng")]
        public string? ShippingAddress { get; set; }

        // "picker" = chọn từ danh sách, "manual" = nhập tay (JavaScript tự đổi khi lỗi tải dữ liệu)
        public string AddressMode { get; set; } = "picker";

        // Lưu số điện thoại + địa chỉ này vào tài khoản cho lần mua sau
        public bool SaveToProfile { get; set; } = true;

        // ===== Chỉ để hiển thị (không nhận từ form) =====
        public CartViewModel Cart { get; set; } = new();
        public string? ReceiverName { get; set; }

        // Địa chỉ đã lưu trong tài khoản nhưng không tách được thành tỉnh/phường (vd: nhập theo kiểu cũ)
        public string? SavedAddressHint { get; set; }
    }
}
