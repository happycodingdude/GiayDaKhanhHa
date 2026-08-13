using ProductionManagement.Application.Contracts;
using ProductionManagement.Domain;
using ProductionManagement.Domain.Services;

namespace ProductionManagement.Application.Common;

/// <summary>The derived order-level values. None of these are stored (Step 3 §13).</summary>
public sealed record OrderDerivedValues(
    int TotalActual,
    int TotalPlan,
    int TotalInitialPlan,
    int Remaining,
    decimal ProgressPercentage,
    ScheduleStatus ScheduleStatus,
    int BehindQuantity,
    int DaysRemaining,
    bool IsOverdue);

public static class OrderDerivedCalculator
{
    /// <summary>
    /// Computes every derived order value from the source data.
    /// </summary>
    /// <param name="plans">(ProductionDate, PlannedQuantity, InitialPlannedQuantity) for the order.</param>
    /// <param name="records">(ProductionDate, ActualQuantity) for the order.</param>
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

        // "Behind schedule" compares the cumulative plan against the cumulative actual over the
        // production days that are already due (master summary §5). It is deliberately not an
        // order status (order list spec §5).
        //
        // Today counts only once its actual has been entered: the actual is recorded at the end of
        // the day, so an order is not late for output that is not due yet. Without this an order
        // would read "behind" every morning, which would make the warning meaningless.
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
        var isOverdue = orderStatus != OrderStatus.Completed && dueDate < today;

        return new OrderDerivedValues(
            TotalActual: totalActual,
            TotalPlan: totalPlan,
            TotalInitialPlan: totalInitialPlan,
            Remaining: ProductionCalculations.Remaining(orderQuantity, totalActual),
            ProgressPercentage: ProductionCalculations.ProgressPercentage(orderQuantity, totalActual),
            ScheduleStatus: scheduleStatus,
            BehindQuantity: behindQuantity,
            DaysRemaining: daysRemaining,
            IsOverdue: isOverdue);
    }
}
