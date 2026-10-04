using System.Globalization;
using Microsoft.AspNetCore.Http.Extensions;

namespace ShoeStore.Models.ViewModels
{
    // Điều kiện tìm kiếm / lọc / sắp xếp trên trang danh sách sản phẩm (/san-pham)
    public class ShopFilter
    {
        // Từ khoá tìm kiếm (không phân biệt dấu): tên sản phẩm, thương hiệu, danh mục
        public string? Q { get; set; }

        public int? CategoryId { get; set; }

        // Chọn được nhiều: ?BrandIds=1&BrandIds=3
        public List<int> BrandIds { get; set; } = new();

        public List<int> SizeIds { get; set; } = new();

        public decimal? MinPrice { get; set; }

        public decimal? MaxPrice { get; set; }

        // Chỉ hiện sản phẩm còn hàng (đã chọn size thì xét đúng size đó)
        public bool InStock { get; set; }

        // newest | bestseller | price_asc | price_desc | name
        public string Sort { get; set; } = "newest";

        public int Page { get; set; } = 1;

        public static readonly Dictionary<string, string> SortOptions = new()
        {
            ["newest"] = "Mới nhất",
            ["bestseller"] = "Bán chạy",
            ["price_asc"] = "Giá thấp → cao",
            ["price_desc"] = "Giá cao → thấp",
            ["name"] = "Tên A → Z"
        };

        public bool IsFiltering =>
            !string.IsNullOrWhiteSpace(Q) || CategoryId.HasValue || BrandIds.Any() || SizeIds.Any()
            || MinPrice.HasValue || MaxPrice.HasValue || InStock;

        // Tạo chuỗi "?Q=...&BrandIds=1&BrandIds=2..." từ bộ lọc hiện tại, có thể sửa vài điều kiện trước.
        // vd: filter.ToQuery(f => f.Page = 2), filter.ToQuery(f => f.BrandIds.Remove(3))
        // Đổi điều kiện lọc thì về trang 1 (trừ khi chính lệnh sửa là đổi trang).
        public string ToQuery(Action<ShopFilter>? change = null)
        {
            var copy = new ShopFilter
            {
                Q = Q,
                CategoryId = CategoryId,
                BrandIds = new List<int>(BrandIds),
                SizeIds = new List<int>(SizeIds),
                MinPrice = MinPrice,
                MaxPrice = MaxPrice,
                InStock = InStock,
                Sort = Sort,
                Page = 1
            };
            change?.Invoke(copy);

            var query = new QueryBuilder();
            if (!string.IsNullOrWhiteSpace(copy.Q)) query.Add("Q", copy.Q.Trim());
            if (copy.CategoryId.HasValue) query.Add("CategoryId", copy.CategoryId.Value.ToString());
            foreach (var id in copy.BrandIds) query.Add("BrandIds", id.ToString());
            foreach (var id in copy.SizeIds) query.Add("SizeIds", id.ToString());
            if (copy.MinPrice.HasValue) query.Add("MinPrice", copy.MinPrice.Value.ToString("0", CultureInfo.InvariantCulture));
            if (copy.MaxPrice.HasValue) query.Add("MaxPrice", copy.MaxPrice.Value.ToString("0", CultureInfo.InvariantCulture));
            if (copy.InStock) query.Add("InStock", "true");
            if (copy.Sort != "newest") query.Add("Sort", copy.Sort);
            if (copy.Page > 1) query.Add("Page", copy.Page.ToString());

            return query.ToString();
        }
    }

    // Một lựa chọn lọc kèm số sản phẩm (danh mục, thương hiệu)
    public class ShopFacet
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    // Dữ liệu trang danh sách sản phẩm
    public class ShopViewModel
    {
        public ShopFilter Filter { get; set; } = new();
        public List<Product> Products { get; set; } = new();

        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public int PageSize { get; set; }

        public List<ShopFacet> Categories { get; set; } = new();
        public List<ShopFacet> Brands { get; set; } = new();
        public List<Size> Sizes { get; set; } = new();
    }
}
