using System.Globalization;

namespace ShoeStore.Models
{
    // Các điều kiện lọc + trang hiện tại trên trang danh sách sản phẩm (admin).
    // Không phải bảng trong database, chỉ dùng để nhận dữ liệu từ form lọc / link phân trang.
    public class ProductFilter
    {
        public string? Keyword { get; set; }

        public int? BrandId { get; set; }

        public int? CategoryId { get; set; }

        public int? SizeId { get; set; }

        public int? ColorId { get; set; }

        // "active" = đang bán, "hidden" = đang ẩn, null = tất cả
        public string? Status { get; set; }

        // "in" = còn hàng, "low" = sắp hết, "out" = hết hàng, null = tất cả
        public string? Stock { get; set; }

        public decimal? MinPrice { get; set; }

        public decimal? MaxPrice { get; set; }

        // Trang hiện tại (bắt đầu từ 1)
        public int Page { get; set; } = 1;

        // Có đang lọc theo điều kiện nào không (không tính số trang)
        public bool IsFiltering =>
            !string.IsNullOrWhiteSpace(Keyword)
            || BrandId.HasValue
            || CategoryId.HasValue
            || SizeId.HasValue
            || ColorId.HasValue
            || !string.IsNullOrEmpty(Status)
            || !string.IsNullOrEmpty(Stock)
            || MinPrice.HasValue
            || MaxPrice.HasValue;

        // Tạo bộ tham số URL giữ nguyên các điều kiện lọc, chỉ đổi số trang.
        // Dùng cho các link phân trang: asp-all-route-data="@filter.ToRouteValues(2)"
        public Dictionary<string, string> ToRouteValues(int page)
        {
            var values = new Dictionary<string, string>();

            if (!string.IsNullOrWhiteSpace(Keyword)) values["Keyword"] = Keyword;
            if (BrandId.HasValue) values["BrandId"] = BrandId.Value.ToString();
            if (CategoryId.HasValue) values["CategoryId"] = CategoryId.Value.ToString();
            if (SizeId.HasValue) values["SizeId"] = SizeId.Value.ToString();
            if (ColorId.HasValue) values["ColorId"] = ColorId.Value.ToString();
            if (!string.IsNullOrEmpty(Status)) values["Status"] = Status;
            if (!string.IsNullOrEmpty(Stock)) values["Stock"] = Stock;
            if (MinPrice.HasValue) values["MinPrice"] = MinPrice.Value.ToString("0", CultureInfo.InvariantCulture);
            if (MaxPrice.HasValue) values["MaxPrice"] = MaxPrice.Value.ToString("0", CultureInfo.InvariantCulture);

            values["Page"] = page.ToString();

            return values;
        }
    }
}
