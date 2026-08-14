namespace ProductionManagement.Domain.Entities;

/// <summary>
/// The add-on allocated to one target production plan by a <see cref="PlanAdjustment"/>.
/// </summary>
public sealed class PlanAdjustmentItem
{
    private PlanAdjustmentItem() { }

    public Guid Id { get; private set; }
    public Guid PlanAdjustmentId { get; private set; }
    public PlanAdjustment PlanAdjustment { get; private set; } = null!;

    /// <summary>The target production plan that receives the add-on.</summary>
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
