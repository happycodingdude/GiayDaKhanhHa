namespace ProductionManagement.Domain.Entities;

/// <summary>
/// Một khoản bù phần thiếu đã áp dụng. Chủ đích không có OrderId — Order được truy ra qua
/// SourceProductionPlan (Step 3 §4.5). Chỉ Apply mới lưu điều chỉnh xuống; Preview thì không.
/// Điều chỉnh Applied là lịch sử bất biến: muốn sửa thì Reverse rồi tạo cái mới.
/// </summary>
public sealed class PlanAdjustment
{
    private readonly List<PlanAdjustmentItem> _items = [];

    private PlanAdjustment() { }

    public Guid Id { get; private set; }
    public Guid SourceProductionPlanId { get; private set; }
    public ProductionPlan SourceProductionPlan { get; private set; } = null!;
    public int ShortageQuantity { get; private set; }
    public AdjustmentType AdjustmentType { get; private set; }
    public AdjustmentStatus Status { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid? AppliedBy { get; private set; }
    public Guid? ReversedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? AppliedAt { get; private set; }
    public DateTimeOffset? ReversedAt { get; private set; }

    public IReadOnlyCollection<PlanAdjustmentItem> Items => _items;

    /// <summary>
    /// Tạo điều chỉnh ở sẵn trạng thái <see cref="AdjustmentStatus.Applied"/> — trạng thái duy nhất
    /// mà một điều chỉnh được lưu xuống (Step 3 §4.5).
    /// </summary>
    public static PlanAdjustment Apply(
        Guid sourceProductionPlanId,
        int shortageQuantity,
        AdjustmentType adjustmentType,
        IReadOnlyList<(Guid ProductionPlanId, int AddOnQuantity)> targets,
        Guid userId,
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

        var seen = new HashSet<Guid>();
        foreach (var (planId, addOn) in targets)
        {
            if (addOn <= 0)
            {
                throw new BusinessRuleException(
                    ErrorCodes.InvalidAdjustmentTarget, "Each add-on quantity must be greater than zero.");
            }

            // UNIQUE(plan_adjustment_id, production_plan_id) — không trùng ngày đích trong một điều chỉnh.
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
            Id = Guid.CreateVersion7(),
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
    /// Applied → Reversed. Lịch sử không bao giờ bị viết lại và một điều chỉnh không thể hoàn tác hai lần.
    /// </summary>
    public void Reverse(Guid userId, DateTimeOffset now)
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
