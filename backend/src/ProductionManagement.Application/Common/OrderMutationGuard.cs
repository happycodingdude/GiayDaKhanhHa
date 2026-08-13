using ProductionManagement.Domain;
using ProductionManagement.Domain.Entities;

namespace ProductionManagement.Application.Common;

/// <summary>
/// Once an order's due date has passed it is frozen: it can be read but no longer changed. This
/// covers completed orders too — the deciding factor is the calendar, not the status. Every use
/// case that writes to an existing order goes through this guard, so the rule cannot be bypassed by
/// calling a different endpoint.
/// </summary>
public static class OrderMutationGuard
{
    /// <summary>
    /// Throws when the order's due date has passed. Call this inside the same transaction that will
    /// do the write, after the order row has been locked, so the decision cannot be made on stale
    /// state.
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
