namespace ShoeStore.Models.ViewModels
{
    // Số liệu trang Dashboard của admin (không phải bảng trong database)
    public class DashboardViewModel
    {
        // ===== Doanh thu: chỉ tính đơn "Hoàn thành" (đã giao, đã thu tiền COD) =====
        public decimal RevenueThisMonth { get; set; }
        public decimal RevenueLastMonth { get; set; }
        public decimal RevenueTotal { get; set; }

        // % tăng/giảm so với tháng trước (null = tháng trước chưa có doanh thu để so)
        public decimal? RevenueGrowth => RevenueLastMonth == 0
            ? null
            : Math.Round((RevenueThisMonth - RevenueLastMonth) / RevenueLastMonth * 100, 1);

        // ===== Đơn hàng =====
        public int OrdersToday { get; set; }
        public int OrdersThisMonth { get; set; }
        public Dictionary<string, int> StatusCounts { get; set; } = new();
        public int PendingOrders => StatusCounts.GetValueOrDefault(OrderStatuses.Pending);

        // ===== Khách hàng =====
        public int CustomersTotal { get; set; }
        public int NewCustomersThisMonth { get; set; }

        // ===== Sản phẩm =====
        public int ActiveProducts { get; set; }
        public int TotalProducts { get; set; }
        public int LowStockCount { get; set; }
        public int OutOfStockCount { get; set; }
        public int LowStockThreshold { get; set; }

        // ===== Bảng / biểu đồ =====
        public List<DashboardDay> Last14Days { get; set; } = new();
        public List<Order> RecentOrders { get; set; } = new();
        public List<DashboardTopProduct> TopProducts { get; set; } = new();
        public List<DashboardStockItem> LowStockProducts { get; set; } = new();
    }

    // 1 cột trên biểu đồ: số đơn và giá trị đơn đặt trong ngày (không tính đơn huỷ)
    public class DashboardDay
    {
        public DateTime Date { get; set; }
        public int Orders { get; set; }
        public decimal Amount { get; set; }
    }

    public class DashboardTopProduct
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Image { get; set; }
        public int Sold { get; set; }
        public decimal Revenue { get; set; }
    }

    public class DashboardStockItem
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Image { get; set; }
        public int Quantity { get; set; }
    }
}
