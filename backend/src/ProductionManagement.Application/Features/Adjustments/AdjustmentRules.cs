using System.Linq.Expressions;
using ProductionManagement.Domain;
using ProductionManagement.Domain.Entities;

namespace ProductionManagement.Application.Features.Adjustments;

/// <summary>
/// Định nghĩa duy nhất về việc ngày sản xuất nào được nhận khoản bù. Đặt ở đây vì cả luồng preview
/// lẫn luồng apply đều phải thống nhất tuyệt đối với nhau.
/// </summary>
public static class AdjustmentRules
{
    /// <summary>
    /// Ngày đích phải là ngày sản xuất sau đó của cùng đơn hàng, không nằm trong quá khứ, và
    /// <b>chưa Xuất hàng</b>: bù vào một ngày đã chốt sổ là viết lại lịch sử của ngày đó
    /// (master summary §8 Rule 7, CR-01 §6.7 / M-07).
    /// </summary>
    public static Expression<Func<ProductionPlan, bool>> EligibleTarget(
        Guid orderId, Guid sourcePlanId, DateOnly sourceDate, DateOnly today,
        IReadOnlyCollection<DateOnly> closedDates)
        => plan => plan.OrderId == orderId
                   && plan.Id != sourcePlanId
                   && plan.ProductionDate > sourceDate
                   && plan.ProductionDate >= today
                   && !closedDates.Contains(plan.ProductionDate);

    /// <summary>
    /// Vì sao một ngày đích do quản lý chọn bị loại. Trả về null khi ngày đó hợp lệ. Mỗi lý do có
    /// mã lỗi riêng để UI nói được đúng nguyên nhân thay vì một câu chung chung (CR-01 §6.7).
    /// </summary>
    public static (string Code, string Message)? RejectionFor(
        DateOnly targetDate, DateOnly sourceDate, DateOnly today, bool isClosed)
    {
        // Thứ tự quan trọng: "không phải ngày sau ngày thiếu" là lý do cụ thể hơn hai lý do còn lại,
        // và nó cũng bắt đúng trường hợp quản lý chọn nhầm chính ngày nguồn (ngày nguồn luôn đã đóng,
        // nên nếu xét trạng thái trước thì thông báo sẽ trỏ sai chỗ).
        if (targetDate <= sourceDate)
        {
            return (ErrorCodes.InvalidAdjustmentTarget,
                "Only a production day after the shortage day can receive the add-on.");
        }

        if (isClosed)
        {
            return (ErrorCodes.TargetDayClosed,
                "This production day has already been closed and can no longer receive an add-on.");
        }

        if (targetDate < today)
        {
            return (ErrorCodes.TargetDateInPast,
                "A production day in the past cannot receive an add-on.");
        }

        return null;
    }
}
