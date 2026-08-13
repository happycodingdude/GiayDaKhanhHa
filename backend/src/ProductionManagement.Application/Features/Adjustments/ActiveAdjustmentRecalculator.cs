using Microsoft.EntityFrameworkCore;
using ProductionManagement.Application.Abstractions;
using ProductionManagement.Application.Contracts;
using ProductionManagement.Domain;
using ProductionManagement.Domain.Entities;
using ProductionManagement.Domain.Services;

namespace ProductionManagement.Application.Features.Adjustments;

/// <summary>
/// Keeps an applied add-on in step with the actual quantity it was based on.
///
/// The shortage of a day is a derived value: correcting that day's actual changes it. An add-on
/// left at the old shortage would keep planning the wrong quantity onto the target days, so when
/// the actual is edited the active adjustment is recalculated from the new shortage.
///
/// An applied adjustment is immutable history (Step 4 §13), so this never edits one: it reverses
/// the outdated adjustment and applies a fresh one, which is the documented way to correct an
/// adjustment. Both entries stay visible in the history.
///
/// The manager's original decision is preserved:
///   Manual    the same target day(s) they chose absorb the new shortage.
///   Automatic the new shortage is spread evenly over the remaining days again.
/// </summary>
public sealed class ActiveAdjustmentRecalculator(
    IAppDbContext db,
    IClock clock,
    ICurrentUser currentUser,
    IAutomaticAllocationStrategy automaticAllocation)
{
    /// <summary>
    /// Must be called inside the transaction that changed the actual, after that change has been
    /// saved — the new shortage is read back from the database. Returns null when there was
    /// nothing to recalculate.
    /// </summary>
    public async Task<AdjustmentRecalculationDto?> RecalculateAsync(
        long orderId, DateOnly productionDate, CancellationToken ct = default)
    {
        var source = await db.ProductionPlans
            .FirstOrDefaultAsync(p => p.OrderId == orderId && p.ProductionDate == productionDate, ct);

        if (source is null)
        {
            return null;
        }

        // At most one adjustment per source day is ever Applied (Step 4 §12).
        var adjustment = await db.PlanAdjustments
            .Include(a => a.Items)
            .FirstOrDefaultAsync(
                a => a.SourceProductionPlanId == source.Id && a.Status == AdjustmentStatus.Applied, ct);

        if (adjustment is null)
        {
            return null;
        }

        var actual = await db.ProductionRecords.AsNoTracking()
            .Where(r => r.OrderId == orderId && r.ProductionDate == productionDate)
            .Select(r => (int?)r.ActualQuantity)
            .FirstOrDefaultAsync(ct);

        var previousShortage = adjustment.ShortageQuantity;
        var newShortage = ProductionCalculations.Shortage(source.PlannedQuantity, actual);

        // An edit that leaves the shortage where it was must not churn the history.
        if (newShortage == previousShortage)
        {
            return null;
        }

        var previousTargetIds = adjustment.Items.Select(i => i.ProductionPlanId).Distinct().ToList();

        var candidateIds = await db.ProductionPlans.AsNoTracking()
            .Where(AdjustmentRules.EligibleTarget(orderId, source.Id, source.ProductionDate, clock.Today))
            .Select(p => p.Id)
            .ToListAsync(ct);

        // Same lock protocol as the manager-driven apply: the caller has already locked the order,
        // then the plans are locked in ascending id order (Step 4 §18).
        var involvedIds = previousTargetIds.Concat(candidateIds).Distinct().ToList();
        await db.LockProductionPlansAsync(involvedIds, ct);

        var plans = await db.ProductionPlans
            .Where(p => involvedIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var now = clock.UtcNow;

        // Applied -> Reversed, and the add-on comes back off the target plans.
        adjustment.Reverse(currentUser.UserId, now);
        foreach (var item in adjustment.Items)
        {
            plans[item.ProductionPlanId].RemoveAddOn(item.AddOnQuantity, now);
        }

        var replacement = newShortage <= 0
            ? null
            : BuildReplacement(adjustment, source, plans, previousTargetIds, candidateIds, newShortage, now);

        if (replacement is not null)
        {
            db.PlanAdjustments.Add(replacement.Value.Adjustment);

            foreach (var target in replacement.Value.Allocation)
            {
                plans[target.ProductionPlanId].AddOn(target.AddOnQuantity, now);
            }
        }

        // Saved here so the replacement has its identity before it is reported back. The caller
        // owns the transaction, so this is still all-or-nothing with the actual that triggered it.
        await db.SaveChangesAsync(ct);

        var outcome = newShortage <= 0
            ? AdjustmentRecalculationOutcome.Removed
            : replacement is null
                ? AdjustmentRecalculationOutcome.Unhandled
                : AdjustmentRecalculationOutcome.Recalculated;

        return new AdjustmentRecalculationDto(
            Outcome: outcome,
            ReversedAdjustmentId: adjustment.Id,
            PreviousShortageQuantity: previousShortage,
            ShortageQuantity: Math.Max(newShortage, 0),
            AdjustmentType: adjustment.AdjustmentType.ToString(),
            AdjustmentId: replacement?.Adjustment.Id,
            Items: replacement is null
                ? []
                : replacement.Value.Allocation
                    .Select(a => new PlanAdjustmentItemDto(a.ProductionPlanId, a.ProductionDate, a.AddOnQuantity))
                    .ToList());
    }

    /// <summary>
    /// Builds the adjustment that replaces the outdated one, or null when the new shortage has
    /// nowhere left to go.
    /// </summary>
    private (PlanAdjustment Adjustment, IReadOnlyList<AllocationResult> Allocation)? BuildReplacement(
        PlanAdjustment previous,
        ProductionPlan source,
        IReadOnlyDictionary<long, ProductionPlan> plans,
        IReadOnlyList<long> previousTargetIds,
        IReadOnlyList<long> candidateIds,
        int newShortage,
        DateTimeOffset now)
    {
        // Manual keeps the manager's chosen days. A chosen day that has since fallen into the past
        // is no longer eligible and is dropped rather than silently adjusted.
        var targetIds = previous.AdjustmentType == AdjustmentType.Automatic
            ? candidateIds
            : previousTargetIds.Where(candidateIds.Contains).ToList();

        if (targetIds.Count == 0)
        {
            return null;
        }

        // The candidates carry each plan as it stands after the old add-on was removed.
        var candidates = targetIds
            .Select(id => new AllocationCandidate(id, plans[id].ProductionDate, plans[id].PlannedQuantity))
            .ToList();

        // Manual with a single chosen day gives that day the whole shortage, which is exactly what
        // the Option 1 workflow does.
        var allocation = automaticAllocation.Allocate(newShortage, candidates);

        var adjustment = PlanAdjustment.Apply(
            source.Id,
            newShortage,
            previous.AdjustmentType,
            allocation.Select(a => (a.ProductionPlanId, a.AddOnQuantity)).ToList(),
            currentUser.UserId,
            now);

        return (adjustment, allocation);
    }
}
