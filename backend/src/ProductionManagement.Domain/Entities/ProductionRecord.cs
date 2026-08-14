namespace ProductionManagement.Domain.Entities;

/// <summary>
/// Sản lượng thực tế sản xuất trong một ngày. Đúng một bản ghi cho mỗi Order + ProductionDate.
/// Thực tế là một giá trị, không phải số cộng thêm: sửa sai là sửa chính bản ghi này (Step 1 §5).
/// Không có bản ghi nghĩa là "chưa nhập", khác hẳn với thực tế bằng 0.
/// </summary>
public sealed class ProductionRecord
{
    private ProductionRecord() { }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Order Order { get; private set; } = null!;
    public DateOnly ProductionDate { get; private set; }
    public int ActualQuantity { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static ProductionRecord Create(
        Guid orderId, DateOnly productionDate, int actualQuantity, Guid userId, DateTimeOffset now)
    {
        GuardQuantity(actualQuantity);

        return new ProductionRecord
        {
            Id = Guid.CreateVersion7(),
            OrderId = orderId,
            ProductionDate = productionDate,
            ActualQuantity = actualQuantity,
            CreatedBy = userId,
            UpdatedBy = userId,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>Thay thế giá trị thực tế. Giá trị cũ không được cộng dồn.</summary>
    public void UpdateActual(int actualQuantity, Guid userId, DateTimeOffset now)
    {
        GuardQuantity(actualQuantity);

        ActualQuantity = actualQuantity;
        UpdatedBy = userId;
        UpdatedAt = now;
    }

    private static void GuardQuantity(int actualQuantity)
    {
        // Số 0 nhập tường minh là hợp lệ; số âm thì không.
        if (actualQuantity < 0)
        {
            throw new ValidationException(
                "actualQuantity", "MUST_BE_GREATER_THAN_OR_EQUAL_TO_ZERO",
                "Actual quantity cannot be negative.");
        }
    }
}
