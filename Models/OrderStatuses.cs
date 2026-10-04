namespace ShoeStore.Models
{
    // Các trạng thái của đơn hàng (giá trị lưu trong cột Orders.Status).
    // Dùng hằng số này thay vì gõ tay "Pending", "Cancelled"... để tránh gõ sai.
    public static class OrderStatuses
    {
        public const string Pending = "Pending";       // Chờ xác nhận (vừa đặt)
        public const string Confirmed = "Confirmed";   // Cửa hàng đã xác nhận
        public const string Shipping = "Shipping";     // Đang giao
        public const string Completed = "Completed";   // Đã giao xong
        public const string Cancelled = "Cancelled";   // Đã huỷ

        // Tên hiển thị tiếng Việt
        public static string DisplayName(string status) => status switch
        {
            Pending => "Chờ xác nhận",
            Confirmed => "Đã xác nhận",
            Shipping => "Đang giao",
            Completed => "Hoàn thành",
            Cancelled => "Đã huỷ",
            _ => status
        };

        public static readonly string[] All = { Pending, Confirmed, Shipping, Completed, Cancelled };

        // Quy trình xử lý đơn: từ trạng thái hiện tại được chuyển sang những trạng thái nào
        // Chờ xác nhận -> Đã xác nhận -> Đang giao -> Hoàn thành; chưa hoàn thành thì đều huỷ được
        public static string[] NextStatuses(string status) => status switch
        {
            Pending => new[] { Confirmed, Cancelled },
            Confirmed => new[] { Shipping, Cancelled },
            Shipping => new[] { Completed, Cancelled },
            _ => Array.Empty<string>()
        };

        // Chữ trên nút chuyển sang trạng thái tương ứng (trang admin)
        public static string ActionName(string nextStatus) => nextStatus switch
        {
            Confirmed => "Xác nhận đơn",
            Shipping => "Giao cho vận chuyển",
            Completed => "Đã giao thành công",
            Cancelled => "Huỷ đơn",
            _ => nextStatus
        };

        // Màu nhãn Bootstrap cho trang admin
        public static string BadgeClass(string status) => status switch
        {
            Pending => "bg-warning text-dark",
            Confirmed => "bg-primary",
            Shipping => "bg-info text-dark",
            Completed => "bg-success",
            Cancelled => "bg-secondary",
            _ => "bg-light text-dark"
        };

        // Class CSS cho nhãn màu của từng trạng thái (trang khách hàng)
        public static string CssClass(string status) => status switch
        {
            Pending => "od-st-pending",
            Confirmed => "od-st-confirmed",
            Shipping => "od-st-shipping",
            Completed => "od-st-completed",
            Cancelled => "od-st-cancelled",
            _ => ""
        };
    }
}
