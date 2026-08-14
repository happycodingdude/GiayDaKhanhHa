using ProductionManagement.Domain;
using ProductionManagement.Domain.Entities;

namespace ProductionManagement.Application.Common;

/// <summary>
/// Khi đơn hàng đã qua ngày hạn thì bị đóng băng: chỉ đọc được, không sửa được nữa. Điều này áp cho
/// cả đơn đã hoàn thành — yếu tố quyết định là lịch, không phải trạng thái. Mọi use case ghi vào một
/// đơn hàng đã tồn tại đều đi qua guard này, nên không thể lách luật bằng cách gọi endpoint khác.
/// </summary>
public static class OrderMutationGuard
{
    /// <summary>
    /// Ném exception khi đơn hàng đã qua ngày hạn. Gọi bên trong đúng transaction sẽ thực hiện ghi,
    /// sau khi đã khóa dòng đơn hàng, để quyết định không dựa trên trạng thái cũ.
    /// </summary>
    public static void EnsureEditable(Order order, DateOnly today)
    {
        if (!order.IsPastDueDateOn(today))
        {
            return;
        }

        throw new BusinessRuleException(
            ErrorCodes.OrderOverdue,
            $"Order '{order.OrderCode}' passed its due date ({order.DueDate:yyyy-MM-dd}) and is read-only. "
            + "Its production data can no longer be changed.");
    }
}
