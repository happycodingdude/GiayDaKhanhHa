namespace ProductionManagement.Domain.Entities;

/// <summary>
/// The actual quantity produced on one day. Exactly one record per Order + ProductionDate.
/// Actual is a value, not an increment: corrections edit this record (Step 1 §5).
/// The absence of a record means "not entered yet" and is distinct from an actual of 0.
/// </summary>
public sealed class ProductionRecord
{
    private ProductionRecord() { }

    public long Id { get; private set; }
    public long OrderId { get; private set; }
    public Order Order { get; private set; } = null!;
    public DateOnly ProductionDate { get; private set; }
    public int ActualQuantity { get; private set; }
    public long CreatedBy { get; private set; }
    public long UpdatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static ProductionRecord Create(
        long orderId, DateOnly productionDate, int actualQuantity, long userId, DateTimeOffset now)
    {
        GuardQuantity(actualQuantity);

        return new ProductionRecord
        {
            OrderId = orderId,
            ProductionDate = productionDate,
            ActualQuantity = actualQuantity,
            CreatedBy = userId,
            UpdatedBy = userId,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>Replaces the actual value. The old value is not accumulated.</summary>
    public void UpdateActual(int actualQuantity, long userId, DateTimeOffset now)
    {
        GuardQuantity(actualQuantity);

        ActualQuantity = actualQuantity;
        UpdatedBy = userId;
        UpdatedAt = now;
    }

    private static void GuardQuantity(int actualQuantity)
    {
        // An explicit 0 is valid; negatives are not.
        if (actualQuantity < 0)
        {
            throw new ValidationException(
                "actualQuantity", "MUST_BE_GREATER_THAN_OR_EQUAL_TO_ZERO",
                "Actual quantity cannot be negative.");
        }
    }
}
