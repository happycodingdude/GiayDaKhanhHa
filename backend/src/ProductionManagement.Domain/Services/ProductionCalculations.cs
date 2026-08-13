namespace ProductionManagement.Domain.Services;

/// <summary>
/// Derived production values. None of these are persisted (Step 3 §13).
/// </summary>
public static class ProductionCalculations
{
    /// <summary>
    /// Shortage for one day = max(PlannedQuantity - ActualQuantity, 0).
    /// A day with no production record has not been entered yet, which is not a shortage.
    /// </summary>
    public static int Shortage(int plannedQuantity, int? actualQuantity)
    {
        if (actualQuantity is null)
        {
            return 0;
        }

        return Math.Max(plannedQuantity - actualQuantity.Value, 0);
    }

    /// <summary>Daily difference = Actual - CurrentPlan. Null while the actual has not been entered.</summary>
    public static int? Difference(int plannedQuantity, int? actualQuantity)
    {
        return actualQuantity is null ? null : actualQuantity.Value - plannedQuantity;
    }

    /// <summary>Remaining = Order.Quantity - TotalActual, never negative.</summary>
    public static int Remaining(int orderQuantity, int totalActual) => Math.Max(orderQuantity - totalActual, 0);

    /// <summary>Progress = TotalActual / Order.Quantity, as a percentage rounded to one decimal.</summary>
    public static decimal ProgressPercentage(int orderQuantity, int totalActual)
    {
        if (orderQuantity <= 0)
        {
            return 0m;
        }

        return Math.Round(totalActual * 100m / orderQuantity, 1, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// How far the order is behind schedule: cumulative plan up to and including today minus
    /// cumulative actual over the same days, floored at zero (master summary §5).
    /// </summary>
    public static int BehindScheduleQuantity(int cumulativePlanToDate, int cumulativeActualToDate)
        => Math.Max(cumulativePlanToDate - cumulativeActualToDate, 0);
}
