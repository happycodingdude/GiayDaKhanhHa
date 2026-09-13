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

/// <summary>Một ô kế hoạch: ngày × dây chuyền (CR-001 §2 QĐ-3).</summary>
public readonly record struct PlanCell(
    DateOnly ProductionDate, Guid ProductionLineId, int PlannedQuantity, int InitialPlannedQuantity);

/// <summary>Sản lượng của một ô sản xuất. <c>IsClosed</c> = ô đã Xuất hàng.</summary>
public readonly record struct ActualCell(
    DateOnly ProductionDate, Guid ProductionLineId, int ActualQuantity, bool IsClosed);

public static class OrderDerivedCalculator
{
    /// <summary>
    /// Tính toàn bộ giá trị suy ra của đơn hàng từ dữ liệu gốc.
    /// </summary>
    /// <param name="dueDate">
    /// Null với đơn chưa lập tiến độ: chưa có ngày kết thúc thì không có "còn bao nhiêu ngày", không
    /// trễ, và không bị đóng băng (CR-001 BR-N04).
    /// </param>
    /// <param name="cells">Các ô kế hoạch của đơn hàng.</param>
    /// <param name="actuals">
    /// Sản lượng của các ô đã có dữ liệu. <c>ActualQuantity</c> là tổng các lần ghi nhận chưa xoá —
    /// bao gồm cả ô còn mở, nên <c>TotalActual</c> là số tạm tính cho tới khi ô cuối được Xuất hàng
    /// (CR-01 §4.5, §6.9).
    /// </param>
    public static OrderDerivedValues Compute(
        int orderQuantity,
        OrderStatus orderStatus,
        DateOnly? dueDate,
        IReadOnlyCollection<PlanCell> cells,
        IReadOnlyCollection<ActualCell> actuals,
        DateOnly today)
    {
        var totalActual = actuals.Sum(d => d.ActualQuantity);
        var totalPlan = cells.Sum(p => p.PlannedQuantity);
        var totalInitialPlan = cells.Sum(p => p.InitialPlannedQuantity);

        // "Chậm tiến độ" so sánh kế hoạch lũy kế với thực tế lũy kế trên những ô sản xuất đã tới hạn
        // (master summary §5). Đây chủ đích không phải là một trạng thái đơn hàng
        // (order list spec §5).
        //
        // Hôm nay chỉ được tính khi ô đó đã Xuất hàng: sản lượng của ô còn mở là số tạm tính và còn
        // tăng tiếp, nên đơn hàng không bị coi là trễ vì sản lượng chưa chốt sổ. Không có điều này
        // thì sáng nào đơn hàng cũng hiện "chậm", làm cảnh báo mất sạch ý nghĩa (CR-01 §4.5).
        //
        // Xét theo từng ô chứ không theo cả ngày: dây chuyền A đã xuất hàng hôm nay thì phần của nó
        // được tính, dây chuyền B chưa xuất thì chưa (CR-001 §2 QĐ-3).
        var closedToday = actuals
            .Where(d => d.ProductionDate == today && d.IsClosed)
            .Select(d => d.ProductionLineId)
            .ToHashSet();

        bool IsDue(DateOnly date, Guid lineId)
            => date < today || (date == today && closedToday.Contains(lineId));

        var cumulativePlanToDate = cells
            .Where(p => IsDue(p.ProductionDate, p.ProductionLineId))
            .Sum(p => p.PlannedQuantity);

        var cumulativeActualToDate = actuals
            .Where(d => IsDue(d.ProductionDate, d.ProductionLineId))
            .Sum(d => d.ActualQuantity);

        var behindQuantity = ProductionCalculations.BehindScheduleQuantity(cumulativePlanToDate, cumulativeActualToDate);

        var scheduleStatus = orderStatus == OrderStatus.Completed
            ? ScheduleStatus.Completed
            : behindQuantity > 0
                ? ScheduleStatus.Behind
                : ScheduleStatus.OnSchedule;

        var daysRemaining = dueDate is null ? 0 : Math.Max(dueDate.Value.DayNumber - today.DayNumber, 0);

        return new OrderDerivedValues(
            TotalActual: totalActual,
            TotalPlan: totalPlan,
            TotalInitialPlan: totalInitialPlan,
            Remaining: ProductionCalculations.Remaining(orderQuantity, totalActual),
            ProgressPercentage: ProductionCalculations.ProgressPercentage(orderQuantity, totalActual),
            ScheduleStatus: scheduleStatus,
            BehindQuantity: behindQuantity,
            DaysRemaining: daysRemaining,
            IsOverdue: Order.IsOverdue(orderStatus, dueDate, today),
            // Không giống IsOverdue: đơn đã hoàn thành thì không trễ, nhưng kỳ sản xuất của nó vẫn
            // kết thúc và dữ liệu vẫn bị đóng băng y như vậy.
            IsPastDueDate: Order.IsPastDueDate(dueDate, today));
    }
}
