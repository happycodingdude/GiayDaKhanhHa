using System.Linq.Expressions;
using ProductionManagement.Domain.Entities;

namespace ProductionManagement.Application.Features.Adjustments;

/// <summary>
/// Định nghĩa duy nhất về việc ngày sản xuất nào được nhận khoản bù. Đặt ở đây vì cả luồng do quản
/// lý kích hoạt (preview/apply) lẫn phần tính lại tự động sau khi sửa thực tế đều phải thống nhất
/// tuyệt đối với nhau.
/// </summary>
public static class AdjustmentRules
{
    /// <summary>
    /// Ngày đích phải là ngày sản xuất sau đó của cùng đơn hàng và không được nằm trong quá khứ:
    /// điều chỉnh một ngày đã diễn ra là viết lại lịch sử (master summary §8 Rule 7, §11).
    /// </summary>
    public static Expression<Func<ProductionPlan, bool>> EligibleTarget(
        Guid orderId, Guid sourcePlanId, DateOnly sourceDate, DateOnly today)
        => plan => plan.OrderId == orderId
                   && plan.Id != sourcePlanId
                   && plan.ProductionDate > sourceDate
                   && plan.ProductionDate >= today;
}
