using System.Linq.Expressions;
using ProductionManagement.Domain;
using ProductionManagement.Domain.Entities;

namespace ProductionManagement.Application.Features.Adjustments;

/// <summary>
/// Định nghĩa duy nhất về việc ô sản xuất nào được nhận khoản bù. Đặt ở đây vì cả luồng preview lẫn
/// luồng apply đều phải thống nhất tuyệt đối với nhau.
/// </summary>
public static class AdjustmentRules
{
    /// <summary>
    /// Ô đích phải thuộc <b>chính dây chuyền</b> phát sinh thiếu, là ngày sản xuất sau đó của cùng
    /// đơn hàng, không nằm trong quá khứ, và <b>chưa Xuất hàng</b>.
    ///
    /// Ràng buộc cùng dây chuyền là luật mới của CR-001 (BR-N12, BR-N13): Phase này không bù chéo
    /// dây chuyền. Bù vào một ô đã chốt sổ là viết lại lịch sử của ô đó
    /// (master summary §8 Rule 7, CR-01 §6.7 / M-07).
    /// </summary>
    public static Expression<Func<ProductionPlan, bool>> EligibleTarget(
        Guid orderId, Guid sourcePlanId, Guid productionLineId, DateOnly sourceDate, DateOnly today,
        IReadOnlyCollection<DateOnly> closedDatesOnLine)
        => plan => plan.OrderId == orderId
                   && plan.ProductionLineId == productionLineId
                   && plan.Id != sourcePlanId
                   && plan.ProductionDate > sourceDate
                   && plan.ProductionDate >= today
                   && !closedDatesOnLine.Contains(plan.ProductionDate);

    /// <summary>
    /// Vì sao một ô đích do quản lý chọn bị loại. Trả về null khi ô đó hợp lệ. Mỗi lý do có mã lỗi
    /// riêng để UI nói được đúng nguyên nhân thay vì một câu chung chung (CR-01 §6.7).
    /// </summary>
    public static (string Code, string Message)? RejectionFor(
        Guid targetLineId, Guid sourceLineId, DateOnly targetDate, DateOnly sourceDate, DateOnly today, bool isClosed)
    {
        // Xét dây chuyền TRƯỚC ngày: một ô của dây chuyền khác có thể tình cờ thoả mọi điều kiện về
        // ngày, và khi đó thông báo "ngày không hợp lệ" sẽ trỏ sai hoàn toàn chỗ sai (BR-N12).
        if (targetLineId != sourceLineId)
        {
            return (ErrorCodes.AdjustmentTargetLineMismatch,
                "A shortage can only be absorbed by the same production line that produced it.");
        }

        // Thứ tự quan trọng: "không phải ngày sau ngày thiếu" là lý do cụ thể hơn hai lý do còn lại,
        // và nó cũng bắt đúng trường hợp quản lý chọn nhầm chính ô nguồn (ô nguồn luôn đã đóng, nên
        // nếu xét trạng thái trước thì thông báo sẽ trỏ sai chỗ).
        if (targetDate <= sourceDate)
        {
            return (ErrorCodes.InvalidAdjustmentTarget,
                "Only a production day after the shortage day can receive the add-on.");
        }

        if (isClosed)
        {
            return (ErrorCodes.TargetDayClosed,
                "This production cell has already been closed and can no longer receive an add-on.");
        }

        if (targetDate < today)
        {
            return (ErrorCodes.TargetDateInPast,
                "A production day in the past cannot receive an add-on.");
        }

        return null;
    }
}
