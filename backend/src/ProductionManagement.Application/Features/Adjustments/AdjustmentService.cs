using Microsoft.EntityFrameworkCore;
using ProductionManagement.Application.Abstractions;
using ProductionManagement.Application.Common;
using ProductionManagement.Application.Contracts;
using ProductionManagement.Domain;
using ProductionManagement.Domain.Entities;
using ProductionManagement.Domain.Services;

namespace ProductionManagement.Application.Features.Adjustments;

/// <summary>
/// Shortage handling. Preview never persists; only Apply creates a PlanAdjustment, and an applied
/// adjustment is immutable history that can only be Reversed (Step 4 §8–§13).
/// </summary>
public sealed class AdjustmentService(
    IAppDbContext db,
    IClock clock,
    ICurrentUser currentUser,
    IAutomaticAllocationStrategy automaticAllocation)
{
    public async Task<AdjustmentPreviewDto> PreviewAsync(
        long productionPlanId, PreviewAdjustmentRequest request, CancellationToken ct = default)
    {
        var adjustmentType = ParseAdjustmentType(request.AdjustmentType);

        var source = await db.ProductionPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productionPlanId, ct)
                     ?? throw new NotFoundException(ErrorCodes.ProductionPlanNotFound, "Production plan was not found.");

        // Preview exists only to prepare an Apply. On an overdue order that Apply can never
        // succeed, so the proposal is refused here rather than offered and then rejected.
        OrderMutationGuard.EnsureEditable(await GetOrderAsync(source.OrderId, ct), clock.Today);

        var (shortage, actual) = await GetShortageAsync(source, ct);
        if (shortage <= 0)
        {
            throw new BusinessRuleException(
                ErrorCodes.NoShortage, "This production day has no shortage to handle.");
        }

        await GuardNoActiveAdjustmentAsync(source.Id, ct);

        var candidates = await GetEligibleTargetsAsync(source, ct);

        List<(long PlanId, DateOnly Date, int Current, int AddOn)> proposal;
        string? validationCode = null;
        string? validationMessage = null;

        if (adjustmentType == AdjustmentType.Automatic)
        {
            // Option 2 — the system distributes the whole shortage across every remaining day.
            var allocation = automaticAllocation.Allocate(
                shortage,
                candidates.Select(c => new AllocationCandidate(c.Id, c.ProductionDate, c.PlannedQuantity)).ToList());

            var byId = candidates.ToDictionary(c => c.Id);
            proposal = allocation
                .Select(a => (a.ProductionPlanId, a.ProductionDate, byId[a.ProductionPlanId].PlannedQuantity, a.AddOnQuantity))
                .ToList();
        }
        else
        {
            // Option 1 — the manager's chosen targets, validated but never silently rewritten.
            var targets = request.Targets ?? [];
            var validation = ValidateManualTargets(targets, candidates, shortage);
            validationCode = validation.Code;
            validationMessage = validation.Message;

            // Only eligible targets are echoed back, so the preview never shows a row the server
            // would refuse. An ineligible selection is reported through the validation message.
            var byId = candidates.ToDictionary(c => c.Id);
            proposal = targets
                .Where(t => byId.ContainsKey(t.ProductionPlanId))
                .Select(t => (
                    t.ProductionPlanId,
                    byId[t.ProductionPlanId].ProductionDate,
                    byId[t.ProductionPlanId].PlannedQuantity,
                    t.AddOnQuantity))
                .ToList();
        }

        var items = proposal
            .OrderBy(p => p.Date)
            .Select(p => new AdjustmentPreviewItemDto(p.PlanId, p.Date, p.Current, p.AddOn, p.Current + p.AddOn))
            .ToList();

        return new AdjustmentPreviewDto(
            SourceProductionPlanId: source.Id,
            SourceProductionDate: source.ProductionDate,
            SourcePlannedQuantity: source.PlannedQuantity,
            SourceActualQuantity: actual,
            ShortageQuantity: shortage,
            AdjustmentType: adjustmentType.ToString(),
            Items: items,
            TotalAddOnQuantity: items.Sum(i => i.AddOnQuantity),
            Valid: validationCode is null,
            ValidationCode: validationCode,
            ValidationMessage: validationMessage);
    }

    public async Task<PlanAdjustmentDto> ApplyAsync(
        long productionPlanId, ApplyAdjustmentRequest request, CancellationToken ct = default)
    {
        var adjustmentType = ParseAdjustmentType(request.AdjustmentType);
        var targets = request.Targets ?? [];

        if (targets.Count == 0)
        {
            throw new ValidationException("targets", "REQUIRED", "At least one target production plan is required.");
        }

        await using var transaction = await db.BeginTransactionAsync(ct);

        var sourceInfo = await db.ProductionPlans.AsNoTracking()
            .Where(p => p.Id == productionPlanId)
            .Select(p => new { p.Id, p.OrderId })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(ErrorCodes.ProductionPlanNotFound, "Production plan was not found.");

        // Lock the order first (it serialises against actual create/edit, which is what determines
        // the shortage), then the plans in ascending id order (Step 4 §18).
        if (!await db.LockOrderAsync(sourceInfo.OrderId, ct))
        {
            throw new NotFoundException(ErrorCodes.OrderNotFound, "Order was not found.");
        }

        OrderMutationGuard.EnsureEditable(await GetOrderAsync(sourceInfo.OrderId, ct), clock.Today);

        var planIdsToLock = targets.Select(t => t.ProductionPlanId).Append(productionPlanId).Distinct().ToList();
        await db.LockProductionPlansAsync(planIdsToLock, ct);

        var source = await db.ProductionPlans.FirstAsync(p => p.Id == productionPlanId, ct);

        // Never trust the preview: recalculate the current shortage from live state (Step 4 §10).
        var (currentShortage, _) = await GetShortageAsync(source, ct);
        if (currentShortage <= 0 || currentShortage != request.ShortageQuantity)
        {
            throw new ConflictException(
                ErrorCodes.AdjustmentOutdated,
                "The adjustment proposal is no longer valid because the source production state has changed.");
        }

        await GuardNoActiveAdjustmentAsync(source.Id, ct);

        var candidates = await GetEligibleTargetsAsync(source, ct);

        var validation = ValidateManualTargets(targets, candidates, currentShortage);
        if (validation.Code is not null)
        {
            throw new BusinessRuleException(validation.Code, validation.Message!);
        }

        if (adjustmentType == AdjustmentType.Automatic)
        {
            // Recompute the automatic proposal and require the submitted one to match it exactly.
            // The server validates the manager's submission rather than silently replacing it.
            var expected = automaticAllocation.Allocate(
                currentShortage,
                candidates.Select(c => new AllocationCandidate(c.Id, c.ProductionDate, c.PlannedQuantity)).ToList());

            var expectedMap = expected.ToDictionary(e => e.ProductionPlanId, e => e.AddOnQuantity);
            var submittedMap = targets.ToDictionary(t => t.ProductionPlanId, t => t.AddOnQuantity);

            if (expectedMap.Count != submittedMap.Count ||
                expectedMap.Any(e => !submittedMap.TryGetValue(e.Key, out var qty) || qty != e.Value))
            {
                throw new ConflictException(
                    ErrorCodes.AdjustmentOutdated,
                    "The proposed allocation no longer matches the current production plan. Request a new preview.");
            }
        }

        var now = clock.UtcNow;

        var adjustment = PlanAdjustment.Apply(
            source.Id,
            currentShortage,
            adjustmentType,
            targets.Select(t => (t.ProductionPlanId, t.AddOnQuantity)).ToList(),
            currentUser.UserId,
            now);

        db.PlanAdjustments.Add(adjustment);

        // Increase the target plans. No other day's plan is ever reduced.
        var targetPlans = await db.ProductionPlans
            .Where(p => planIdsToLock.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        foreach (var target in targets)
        {
            targetPlans[target.ProductionPlanId].AddOn(target.AddOnQuantity, now);
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await GetAdjustmentDtoAsync(adjustment.Id, ct);
    }

    public async Task<PlanAdjustmentDto> ReverseAsync(long adjustmentId, CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);

        if (!await db.LockPlanAdjustmentAsync(adjustmentId, ct))
        {
            throw new NotFoundException(ErrorCodes.AdjustmentNotFound, "Plan adjustment was not found.");
        }

        var adjustment = await db.PlanAdjustments
            .Include(a => a.Items)
            .FirstAsync(a => a.Id == adjustmentId, ct);

        // plan_adjustments has no order_id; the order is reached through the source production plan.
        var sourceOrderId = await db.ProductionPlans.AsNoTracking()
            .Where(p => p.Id == adjustment.SourceProductionPlanId)
            .Select(p => p.OrderId)
            .FirstAsync(ct);

        OrderMutationGuard.EnsureEditable(await GetOrderAsync(sourceOrderId, ct), clock.Today);

        var affectedPlanIds = adjustment.Items.Select(i => i.ProductionPlanId).Distinct().ToList();
        await db.LockProductionPlansAsync(affectedPlanIds, ct);

        // Applied → Reversed only. A reversed adjustment can never be reversed again.
        adjustment.Reverse(currentUser.UserId, clock.UtcNow);

        var plans = await db.ProductionPlans
            .Where(p => affectedPlanIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var now = clock.UtcNow;
        foreach (var item in adjustment.Items)
        {
            plans[item.ProductionPlanId].RemoveAddOn(item.AddOnQuantity, now);
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await GetAdjustmentDtoAsync(adjustment.Id, ct);
    }

    public async Task<IReadOnlyList<PlanAdjustmentDto>> GetHistoryAsync(long orderId, CancellationToken ct = default)
    {
        if (!await db.Orders.AnyAsync(o => o.Id == orderId, ct))
        {
            throw new NotFoundException(ErrorCodes.OrderNotFound, "Order was not found.");
        }

        // The order is reached through the source production plan; plan_adjustments has no order_id.
        var adjustmentIds = await db.PlanAdjustments.AsNoTracking()
            .Where(a => db.ProductionPlans.Any(p => p.Id == a.SourceProductionPlanId && p.OrderId == orderId))
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Select(a => a.Id)
            .ToListAsync(ct);

        return await BuildAdjustmentDtosAsync(adjustmentIds, ct);
    }

    private async Task<Order> GetOrderAsync(long orderId, CancellationToken ct)
        => await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId, ct)
           ?? throw new NotFoundException(ErrorCodes.OrderNotFound, "Order was not found.");

    private async Task<PlanAdjustmentDto> GetAdjustmentDtoAsync(long adjustmentId, CancellationToken ct)
    {
        var dtos = await BuildAdjustmentDtosAsync([adjustmentId], ct);
        return dtos[0];
    }

    private async Task<IReadOnlyList<PlanAdjustmentDto>> BuildAdjustmentDtosAsync(
        IReadOnlyList<long> adjustmentIds, CancellationToken ct)
    {
        if (adjustmentIds.Count == 0)
        {
            return [];
        }

        var adjustments = await db.PlanAdjustments.AsNoTracking()
            .Where(a => adjustmentIds.Contains(a.Id))
            .Include(a => a.Items)
            .ToListAsync(ct);

        var planIds = adjustments
            .SelectMany(a => a.Items.Select(i => i.ProductionPlanId))
            .Concat(adjustments.Select(a => a.SourceProductionPlanId))
            .Distinct()
            .ToList();

        var planDates = await db.ProductionPlans.AsNoTracking()
            .Where(p => planIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.ProductionDate, ct);

        var userIds = adjustments
            .SelectMany(a => new[] { (long?)a.CreatedBy, a.AppliedBy, a.ReversedBy })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var userNames = userIds.Count == 0
            ? []
            : await db.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        var order = adjustmentIds.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);

        return adjustments
            .OrderBy(a => order[a.Id])
            .Select(a => new PlanAdjustmentDto(
                a.Id,
                a.SourceProductionPlanId,
                planDates.GetValueOrDefault(a.SourceProductionPlanId),
                a.ShortageQuantity,
                a.AdjustmentType.ToString(),
                a.Status.ToString(),
                a.Items
                    .Select(i => new PlanAdjustmentItemDto(
                        i.ProductionPlanId, planDates.GetValueOrDefault(i.ProductionPlanId), i.AddOnQuantity))
                    .OrderBy(i => i.ProductionDate)
                    .ToList(),
                userNames.GetValueOrDefault(a.CreatedBy, "—"),
                a.AppliedBy.HasValue ? userNames.GetValueOrDefault(a.AppliedBy.Value) : null,
                a.ReversedBy.HasValue ? userNames.GetValueOrDefault(a.ReversedBy.Value) : null,
                a.CreatedAt,
                a.AppliedAt,
                a.ReversedAt))
            .ToList();
    }

    /// <summary>Shortage for the source day. Requires an actual to have been entered.</summary>
    private async Task<(int Shortage, int? Actual)> GetShortageAsync(ProductionPlan source, CancellationToken ct)
    {
        var actual = await db.ProductionRecords.AsNoTracking()
            .Where(r => r.OrderId == source.OrderId && r.ProductionDate == source.ProductionDate)
            .Select(r => (int?)r.ActualQuantity)
            .FirstOrDefaultAsync(ct);

        return (ProductionCalculations.Shortage(source.PlannedQuantity, actual), actual);
    }

    private async Task GuardNoActiveAdjustmentAsync(long sourceProductionPlanId, CancellationToken ct)
    {
        // At most one Applied adjustment per source plan (Step 4 §12). This also makes a duplicate
        // apply after a network retry impossible without an idempotency table.
        var hasActive = await db.PlanAdjustments
            .AnyAsync(a => a.SourceProductionPlanId == sourceProductionPlanId && a.Status == AdjustmentStatus.Applied, ct);

        if (hasActive)
        {
            throw new ConflictException(
                ErrorCodes.ActiveAdjustmentExists,
                "This production day already has an active adjustment. Reverse it before creating a new one.");
        }
    }

    /// <summary>
    /// Plans that may receive an add-on: after the shortage day and not in the past. Adjusting a
    /// past day would rewrite history (master summary §8 Rule 7, §11).
    /// </summary>
    private async Task<List<ProductionPlan>> GetEligibleTargetsAsync(ProductionPlan source, CancellationToken ct)
    {
        var candidates = await db.ProductionPlans.AsNoTracking()
            .Where(AdjustmentRules.EligibleTarget(source.OrderId, source.Id, source.ProductionDate, clock.Today))
            .OrderBy(p => p.ProductionDate)
            .ToListAsync(ct);

        if (candidates.Count == 0)
        {
            throw new BusinessRuleException(
                ErrorCodes.NoEligibleTargetPlans,
                "There is no remaining production day that can absorb this shortage.");
        }

        return candidates;
    }

    private static (string? Code, string? Message) ValidateManualTargets(
        IReadOnlyList<AdjustmentTargetRequest> targets,
        IReadOnlyList<ProductionPlan> candidates,
        int shortage)
    {
        if (targets.Count == 0)
        {
            return (ErrorCodes.InvalidAdjustmentTarget, "Select at least one production day to absorb the shortage.");
        }

        var eligibleIds = candidates.Select(c => c.Id).ToHashSet();
        var seen = new HashSet<long>();

        foreach (var target in targets)
        {
            if (target.AddOnQuantity <= 0)
            {
                return (ErrorCodes.InvalidAdjustmentTarget, "Each add-on quantity must be greater than zero.");
            }

            if (!eligibleIds.Contains(target.ProductionPlanId))
            {
                return (ErrorCodes.InvalidAdjustmentTarget,
                    "A selected production day cannot receive this add-on. Only later days that are not in the past are eligible.");
            }

            if (!seen.Add(target.ProductionPlanId))
            {
                return (ErrorCodes.DuplicateAdjustmentTarget,
                    "The same production day cannot appear twice in one adjustment.");
            }
        }

        var total = targets.Sum(t => (long)t.AddOnQuantity);
        if (total != shortage)
        {
            return (ErrorCodes.AdjustmentTotalMismatch,
                $"The total add-on quantity ({total}) must equal the shortage quantity ({shortage}).");
        }

        return (null, null);
    }

    private static AdjustmentType ParseAdjustmentType(string? value)
    {
        if (!Enum.TryParse<AdjustmentType>(value, ignoreCase: true, out var parsed))
        {
            throw new ValidationException(
                "adjustmentType", "INVALID_VALUE", "Adjustment type must be 'Manual' or 'Automatic'.");
        }

        return parsed;
    }
}
