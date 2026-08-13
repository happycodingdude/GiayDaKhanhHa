namespace ProductionManagement.Domain.Entities;

/// <summary>
/// An applied shortage add-on. There is intentionally no OrderId — the Order is reached through
/// SourceProductionPlan (Step 3 §4.5). Only Apply persists an adjustment; Preview never does.
/// An Applied adjustment is immutable history: correcting it means Reverse + create a new one.
/// </summary>
public sealed class PlanAdjustment
{
    private readonly List<PlanAdjustmentItem> _items = [];

    private PlanAdjustment() { }

    public long Id { get; private set; }
    public long SourceProductionPlanId { get; private set; }
    public ProductionPlan SourceProductionPlan { get; private set; } = null!;
    public int ShortageQuantity { get; private set; }
    public AdjustmentType AdjustmentType { get; private set; }
    public AdjustmentStatus Status { get; private set; }
    public long CreatedBy { get; private set; }
    public long? AppliedBy { get; private set; }
    public long? ReversedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? AppliedAt { get; private set; }
    public DateTimeOffset? ReversedAt { get; private set; }

    public IReadOnlyCollection<PlanAdjustmentItem> Items => _items;

    /// <summary>
    /// Creates an adjustment already in the <see cref="AdjustmentStatus.Applied"/> state — the only
    /// state an adjustment is ever persisted in (Step 3 §4.5).
    /// </summary>
    public static PlanAdjustment Apply(
        long sourceProductionPlanId,
        int shortageQuantity,
        AdjustmentType adjustmentType,
        IReadOnlyList<(long ProductionPlanId, int AddOnQuantity)> targets,
        long userId,
        DateTimeOffset now)
    {
        if (shortageQuantity <= 0)
        {
            throw new BusinessRuleException(
                ErrorCodes.NoShortage, "An adjustment requires a shortage quantity greater than zero.");
        }

        if (targets.Count == 0)
        {
            throw new BusinessRuleException(
                ErrorCodes.InvalidAdjustmentTarget, "An adjustment requires at least one target production plan.");
        }

        var seen = new HashSet<long>();
        foreach (var (planId, addOn) in targets)
        {
            if (addOn <= 0)
            {
                throw new BusinessRuleException(
                    ErrorCodes.InvalidAdjustmentTarget, "Each add-on quantity must be greater than zero.");
            }

            // UNIQUE(plan_adjustment_id, production_plan_id) — no duplicate target in one adjustment.
            if (!seen.Add(planId))
            {
                throw new BusinessRuleException(
                    ErrorCodes.DuplicateAdjustmentTarget,
                    "The same target production plan cannot appear twice in one adjustment.");
            }
        }

        // SUM(Item.AddOnQuantity) == Adjustment.ShortageQuantity (Step 3 §11).
        var totalAddOn = targets.Sum(t => (long)t.AddOnQuantity);
        if (totalAddOn != shortageQuantity)
        {
            throw new BusinessRuleException(
                ErrorCodes.AdjustmentTotalMismatch,
                $"The total add-on quantity ({totalAddOn}) must equal the shortage quantity ({shortageQuantity}).");
        }

        var adjustment = new PlanAdjustment
        {
            SourceProductionPlanId = sourceProductionPlanId,
            ShortageQuantity = shortageQuantity,
            AdjustmentType = adjustmentType,
            Status = AdjustmentStatus.Applied,
            CreatedBy = userId,
            AppliedBy = userId,
            CreatedAt = now,
            AppliedAt = now
        };

        foreach (var (planId, addOn) in targets)
        {
            adjustment._items.Add(PlanAdjustmentItem.Create(adjustment, planId, addOn));
        }

        return adjustment;
    }

    /// <summary>
    /// Applied → Reversed. History is never rewritten and an adjustment cannot be reversed twice.
    /// </summary>
    public void Reverse(long userId, DateTimeOffset now)
    {
        if (Status != AdjustmentStatus.Applied)
        {
            throw new ConflictException(
                ErrorCodes.AdjustmentNotApplied,
                "Only an applied adjustment can be reversed.");
        }

        Status = AdjustmentStatus.Reversed;
        ReversedBy = userId;
        ReversedAt = now;
    }
}
