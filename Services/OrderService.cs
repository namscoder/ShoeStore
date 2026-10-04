using Microsoft.EntityFrameworkCore;
using ShoeStore.Data;
using ShoeStore.Models;

namespace ShoeStore.Services
{
    // Đổi trạng thái đơn hàng. Dùng chung cho khách (huỷ đơn) và admin (xác nhận, giao, hoàn thành, huỷ).
    public class OrderService
    {
        private readonly ApplicationDbContext _context;

        public OrderService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Chuyển đơn từ fromStatus sang toStatus. Trả về false nếu đơn không còn ở fromStatus
        // (vd: 2 admin cùng bấm, hoặc khách vừa huỷ đúng lúc admin xác nhận) => không làm gì cả.
        // Huỷ đơn thì trả hàng của đơn về kho. Mọi việc nằm trong 1 giao dịch.
        public async Task<bool> ChangeStatusAsync(int orderId, string fromStatus, string toStatus)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            // Đổi trạng thái NGAY TRONG CÂU SQL kèm điều kiện "đang ở fromStatus":
            // chỉ 1 người đổi được, người đến sau nhận 0 dòng bị sửa
            var updated = await _context.Orders
                .Where(o => o.Id == orderId && o.Status == fromStatus)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, toStatus));

            if (updated == 0)
            {
                return false; // chưa Commit => giao dịch tự huỷ
            }

            if (toStatus == OrderStatuses.Cancelled)
            {
                var details = await _context.OrderDetails
                    .Where(d => d.OrderId == orderId)
                    .Select(d => new { d.ProductVariantId, d.Quantity })
                    .ToListAsync();

                foreach (var detail in details)
                {
                    await _context.ProductVariants
                        .Where(v => v.Id == detail.ProductVariantId)
                        .ExecuteUpdateAsync(s => s.SetProperty(v => v.Quantity, v => v.Quantity + detail.Quantity));
                }
            }

            await transaction.CommitAsync();
            return true;
        }
    }
}
