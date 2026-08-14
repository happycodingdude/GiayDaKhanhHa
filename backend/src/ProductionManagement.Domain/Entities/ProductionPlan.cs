namespace ProductionManagement.Domain.Entities;

/// <summary>
/// The plan for one production day. <see cref="InitialPlannedQuantity"/> is immutable;
/// <see cref="PlannedQuantity"/> is the current plan after add-on adjustments (Step 1 §4).
/// </summary>
public sealed class ProductionPlan
{
    private ProductionPlan() { }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Order Order { get; private set; } = null!;
    public DateOnly ProductionDate { get; private set; }
    public int InitialPlannedQuantity { get; private set; }
    public int PlannedQuantity { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    internal static ProductionPlan Create(Order order, DateOnly productionDate, int plannedQuantity, DateTimeOffset now)
    {
        return new ProductionPlan
        {
            Id = Guid.CreateVersion7(),
            Order = order,
            ProductionDate = productionDate,
            InitialPlannedQuantity = plannedQuantity,
            PlannedQuantity = plannedQuantity,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Applies an add-on from a plan adjustment. Adjustments only ever increase a plan; they never
    /// reduce another day's plan (master summary §8 Rule 3).
    /// </summary>
    public void AddOn(int quantity, DateTimeOffset now)
    {
        if (quantity <= 0)
        {
            throw new BusinessRuleException(
                ErrorCodes.InvalidAdjustmentTarget, "Add-on quantity must be greater than zero.");
        }

        PlannedQuantity += quantity;
        UpdatedAt = now;
    }

    /// <summary>Removes a previously applied add-on when its adjustment is reversed.</summary>
    public void RemoveAddOn(int quantity, DateTimeOffset now)
    {
        if (quantity <= 0)
        {
            throw new BusinessRuleException(
                ErrorCodes.InvalidAdjustmentTarget, "Add-on quantity must be greater than zero.");
        }

        // planned_quantity >= 0 is a database CHECK constraint; guard before it can be violated.
        if (PlannedQuantity - quantity < 0)
        {
            throw new ConflictException(
                ErrorCodes.AdjustmentOutdated,
                "Reversing this adjustment would make the planned quantity negative.");
        }

        PlannedQuantity -= quantity;
        UpdatedAt = now;
    }
}
