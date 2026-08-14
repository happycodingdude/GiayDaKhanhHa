namespace ProductionManagement.Domain.Entities;

/// <summary>
/// Kế hoạch của một ngày sản xuất. <see cref="InitialPlannedQuantity"/> là bất biến;
/// <see cref="PlannedQuantity"/> là kế hoạch hiện tại sau các điều chỉnh bù (Step 1 §4).
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
    /// Áp dụng khoản bù từ một điều chỉnh kế hoạch. Điều chỉnh chỉ làm tăng kế hoạch; không bao giờ
    /// giảm kế hoạch của ngày khác (master summary §8 Rule 3).
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

    /// <summary>Gỡ khoản bù đã áp dụng trước đó khi điều chỉnh của nó bị hoàn tác.</summary>
    public void RemoveAddOn(int quantity, DateTimeOffset now)
    {
        if (quantity <= 0)
        {
            throw new BusinessRuleException(
                ErrorCodes.InvalidAdjustmentTarget, "Add-on quantity must be greater than zero.");
        }

        // planned_quantity >= 0 là ràng buộc CHECK của database; chặn trước khi nó bị vi phạm.
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
