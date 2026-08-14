namespace ProductionManagement.Domain.Entities;

/// <summary>
/// Khoản bù mà một <see cref="PlanAdjustment"/> phân bổ cho một kế hoạch sản xuất đích.
/// </summary>
public sealed class PlanAdjustmentItem
{
    private PlanAdjustmentItem() { }

    public Guid Id { get; private set; }
    public Guid PlanAdjustmentId { get; private set; }
    public PlanAdjustment PlanAdjustment { get; private set; } = null!;

    /// <summary>Kế hoạch sản xuất đích nhận khoản bù.</summary>
    public Guid ProductionPlanId { get; private set; }
    public ProductionPlan ProductionPlan { get; private set; } = null!;

    public int AddOnQuantity { get; private set; }

    internal static PlanAdjustmentItem Create(PlanAdjustment adjustment, Guid productionPlanId, int addOnQuantity)
    {
        return new PlanAdjustmentItem
        {
            Id = Guid.CreateVersion7(),
            PlanAdjustment = adjustment,
            ProductionPlanId = productionPlanId,
            AddOnQuantity = addOnQuantity
        };
    }
}
