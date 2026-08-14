using ProductionManagement.Application.Contracts;
using ProductionManagement.Domain;
using ProductionManagement.Domain.Entities;
using ProductionManagement.Domain.Services;

namespace ProductionManagement.Application.Common;

/// <summary>Các giá trị suy ra ở mức đơn hàng. Không giá trị nào được lưu xuống (Step 3 §13).</summary>
public sealed record OrderDerivedValues(
    int TotalActual,
    int TotalPlan,
    int TotalInitialPlan,
    int Remaining,
    decimal ProgressPercentage,
    ScheduleStatus ScheduleStatus,
    int BehindQuantity,
    int DaysRemaining,
    bool IsOverdue,
    bool IsPastDueDate);

public static class OrderDerivedCalculator
{
    /// <summary>
    /// Tính toàn bộ giá trị suy ra của đơn hàng từ dữ liệu gốc.
    /// </summary>
    /// <param name="plans">(ProductionDate, PlannedQuantity, InitialPlannedQuantity) của đơn hàng.</param>
    /// <param name="records">(ProductionDate, ActualQuantity) của đơn hàng.</param>
    public static OrderDerivedValues Compute(
        int orderQuantity,
        OrderStatus orderStatus,
        DateOnly dueDate,
        IReadOnlyCollection<(DateOnly ProductionDate, int PlannedQuantity, int InitialPlannedQuantity)> plans,
        IReadOnlyCollection<(DateOnly ProductionDate, int ActualQuantity)> records,
        DateOnly today)
    {
        var totalActual = records.Sum(r => r.ActualQuantity);
        var totalPlan = plans.Sum(p => p.PlannedQuantity);
        var totalInitialPlan = plans.Sum(p => p.InitialPlannedQuantity);

        // "Chậm tiến độ" so sánh kế hoạch lũy kế với thực tế lũy kế trên những ngày sản xuất đã tới
        // hạn (master summary §5). Đây chủ đích không phải là một trạng thái đơn hàng
        // (order list spec §5).
        //
        // Hôm nay chỉ được tính khi đã nhập thực tế: thực tế ghi vào cuối ngày, nên đơn hàng không
        // bị coi là trễ vì sản lượng chưa tới hạn. Không có điều này thì sáng nào đơn hàng cũng hiện
        // "chậm", làm cảnh báo mất sạch ý nghĩa.
        var recordDates = records.Select(r => r.ProductionDate).ToHashSet();
        bool IsDue(DateOnly date) => date < today || (date == today && recordDates.Contains(date));

        var cumulativePlanToDate = plans.Where(p => IsDue(p.ProductionDate)).Sum(p => p.PlannedQuantity);
        var cumulativeActualToDate = records.Where(r => IsDue(r.ProductionDate)).Sum(r => r.ActualQuantity);
        var behindQuantity = ProductionCalculations.BehindScheduleQuantity(cumulativePlanToDate, cumulativeActualToDate);

        var scheduleStatus = orderStatus == OrderStatus.Completed
            ? ScheduleStatus.Completed
            : behindQuantity > 0
                ? ScheduleStatus.Behind
                : ScheduleStatus.OnSchedule;

        var daysRemaining = Math.Max(dueDate.DayNumber - today.DayNumber, 0);
        var isOverdue = Order.IsOverdue(orderStatus, dueDate, today);

        return new OrderDerivedValues(
            TotalActual: totalActual,
            TotalPlan: totalPlan,
            TotalInitialPlan: totalInitialPlan,
            Remaining: ProductionCalculations.Remaining(orderQuantity, totalActual),
            ProgressPercentage: ProductionCalculations.ProgressPercentage(orderQuantity, totalActual),
            ScheduleStatus: scheduleStatus,
            BehindQuantity: behindQuantity,
            DaysRemaining: daysRemaining,
            IsOverdue: isOverdue,
            // Không giống IsOverdue: đơn đã hoàn thành thì không trễ, nhưng kỳ sản xuất của nó vẫn
            // kết thúc và dữ liệu vẫn bị đóng băng y như vậy.
            IsPastDueDate: Order.IsPastDueDate(dueDate, today));
    }
}
