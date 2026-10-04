using Microsoft.EntityFrameworkCore;
using ShoeStore.Data;
using ShoeStore.Models;
using ShoeStore.Models.ViewModels;

namespace ShoeStore.Services
{
    // Thông tin tổng quan của tài khoản (tên, email, thống kê đơn hàng).
    // Dùng chung cho trang Tài khoản và cột menu bên trái (ProfileNavViewComponent).
    public class ProfileService
    {
        private readonly ApplicationDbContext _context;

        public ProfileService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ProfileSummary?> GetSummaryAsync(int userId)
        {
            var user = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.Username, u.FullName, u.Email, u.Role, u.CreatedAt })
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return null;
            }

            // Đếm đơn theo trạng thái trong 1 câu SQL
            var byStatus = await _context.Orders
                .Where(o => o.UserId == userId)
                .GroupBy(o => o.Status)
                .Select(g => new { Status = g.Key, Count = g.Count(), Total = g.Sum(o => o.TotalAmount) })
                .ToListAsync();

            int CountOf(string status) => byStatus.Where(x => x.Status == status).Sum(x => x.Count);

            return new ProfileSummary
            {
                Username = user.Username,
                FullName = user.FullName ?? user.Username,
                Email = user.Email,
                Role = user.Role,
                CreatedAt = user.CreatedAt,
                TotalOrders = byStatus.Sum(x => x.Count),
                ProcessingOrders = CountOf(OrderStatuses.Pending) + CountOf(OrderStatuses.Confirmed) + CountOf(OrderStatuses.Shipping),
                CompletedOrders = CountOf(OrderStatuses.Completed),
                CancelledOrders = CountOf(OrderStatuses.Cancelled),
                TotalSpent = byStatus.Where(x => x.Status == OrderStatuses.Completed).Sum(x => x.Total)
            };
        }
    }
}
