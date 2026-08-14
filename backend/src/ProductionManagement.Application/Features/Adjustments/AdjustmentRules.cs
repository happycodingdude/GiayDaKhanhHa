using System.Linq.Expressions;
using ProductionManagement.Domain.Entities;

namespace ProductionManagement.Application.Features.Adjustments;

/// <summary>
/// The single definition of which production days may receive an add-on. Kept here because both
/// the manager-driven workflow (preview/apply) and the automatic recalculation that follows an
/// edited actual must agree on it exactly.
/// </summary>
public static class AdjustmentRules
{
    /// <summary>
    /// A target must be a later production day of the same order and must not be in the past:
    /// adjusting a day that has already happened would rewrite history
    /// (master summary §8 Rule 7, §11).
    /// </summary>
    public static Expression<Func<ProductionPlan, bool>> EligibleTarget(
        Guid orderId, Guid sourcePlanId, DateOnly sourceDate, DateOnly today)
        => plan => plan.OrderId == orderId
                   && plan.Id != sourcePlanId
                   && plan.ProductionDate > sourceDate
                   && plan.ProductionDate >= today;
}
