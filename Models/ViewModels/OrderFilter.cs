namespace ShoeStore.Models.ViewModels
{
    // Điều kiện lọc + trang hiện tại trên trang Quản lý đơn hàng (admin)
    public class OrderFilter
    {
        // Mã đơn (vd: 12 hoặc #12), tên / tên đăng nhập / email / số điện thoại khách
        public string? Keyword { get; set; }

        // Một trong OrderStatuses.All, null = tất cả
        public string? Status { get; set; }

        public DateTime? FromDate { get; set; }

        public DateTime? ToDate { get; set; }

        public int Page { get; set; } = 1;

        public bool IsFiltering =>
            !string.IsNullOrWhiteSpace(Keyword) || FromDate.HasValue || ToDate.HasValue;

        // Bộ tham số URL giữ nguyên điều kiện lọc, đổi số trang / trạng thái
        public Dictionary<string, string> ToRouteValues(int page, string? status = null, bool keepStatus = true)
        {
            var values = new Dictionary<string, string>();

            if (!string.IsNullOrWhiteSpace(Keyword)) values["Keyword"] = Keyword;
            if (FromDate.HasValue) values["FromDate"] = FromDate.Value.ToString("yyyy-MM-dd");
            if (ToDate.HasValue) values["ToDate"] = ToDate.Value.ToString("yyyy-MM-dd");

            var statusValue = keepStatus ? Status : status;
            if (!string.IsNullOrEmpty(statusValue)) values["Status"] = statusValue;

            values["Page"] = page.ToString();
            return values;
        }
    }
}
