namespace ShoeStore.Models.ViewModels
{
    // Điều kiện lọc + trang hiện tại trên trang danh sách tài khoản (admin)
    public class UserFilter
    {
        // Tìm trong tên đăng nhập, email, họ tên, số điện thoại
        public string? Keyword { get; set; }

        // AppRoles.Admin / AppRoles.Customer, null = tất cả
        public string? Role { get; set; }

        // "active" = đang hoạt động, "locked" = đã khoá, null = tất cả
        public string? Status { get; set; }

        public int Page { get; set; } = 1;

        public bool IsFiltering =>
            !string.IsNullOrWhiteSpace(Keyword)
            || !string.IsNullOrEmpty(Role)
            || !string.IsNullOrEmpty(Status);

        // Bộ tham số URL giữ nguyên điều kiện lọc, chỉ đổi số trang (dùng cho link phân trang)
        public Dictionary<string, string> ToRouteValues(int page)
        {
            var values = new Dictionary<string, string>();

            if (!string.IsNullOrWhiteSpace(Keyword)) values["Keyword"] = Keyword;
            if (!string.IsNullOrEmpty(Role)) values["Role"] = Role;
            if (!string.IsNullOrEmpty(Status)) values["Status"] = Status;

            values["Page"] = page.ToString();

            return values;
        }
    }
}
