namespace ProductionManagement.Domain.Entities;

/// <summary>
/// Một ngày sản xuất của đơn hàng. Đúng một bản ghi cho mỗi Order + ProductionDate, được tạo lazily
/// ở lần ghi nhận đầu tiên hoặc khi Xuất hàng cho ngày chưa có dòng nào (CR-01 §14.4).
///
/// Sản lượng thực tế của ngày là tổng các <see cref="ProductionEntry"/> chưa xoá, không phải một
/// giá trị nhập tay. <see cref="ActualQuantity"/> chỉ là ảnh chụp tại thời điểm đóng ngày.
/// Ngày đã đóng là bất biến: không sửa, không xoá, không mở lại (CR-01 §4.4).
/// </summary>
public sealed class ProductionDay
{
    private readonly List<ProductionEntry> _entries = [];

    private ProductionDay() { }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Order Order { get; private set; } = null!;
    public DateOnly ProductionDate { get; private set; }
    public ProductionDayStatus Status { get; private set; }

    /// <summary>Null khi ngày còn mở. Chỉ có giá trị sau khi Xuất hàng (CR-01 OV-9).</summary>
    public int? ActualQuantity { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }
    public Guid? ClosedBy { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<ProductionEntry> Entries => _entries;

    public bool IsClosed => Status == ProductionDayStatus.Closed;

    public static ProductionDay Open(Guid orderId, DateOnly productionDate, Guid userId, DateTimeOffset now)
        => new()
        {
            Id = Guid.CreateVersion7(),
            OrderId = orderId,
            ProductionDate = productionDate,
            Status = ProductionDayStatus.Open,
            ActualQuantity = null,
            CreatedBy = userId,
            UpdatedBy = userId,
            CreatedAt = now,
            UpdatedAt = now
        };

    /// <summary>
    /// Chốt sổ ngày sản xuất. Sản lượng thực tế do server tự tính từ các lần ghi nhận — client không
    /// bao giờ gửi lên con số này (CR-01 §6.6, §14.1).
    /// </summary>
    public void Close(int actualQuantity, Guid userId, DateTimeOffset now)
    {
        EnsureOpen();

        if (actualQuantity < 0)
        {
            throw new ValidationException(
                "actualQuantity", "MUST_BE_GREATER_THAN_OR_EQUAL_TO_ZERO", "Actual quantity cannot be negative.");
        }

        Status = ProductionDayStatus.Closed;
        ActualQuantity = actualQuantity;
        ClosedAt = now;
        ClosedBy = userId;
        UpdatedBy = userId;
        UpdatedAt = now;
    }

    /// <summary>Ném 409 khi ngày đã đóng. Mọi thao tác lên entry đều phải đi qua đây (CR-01 N-04).</summary>
    public void EnsureOpen()
    {
        if (IsClosed)
        {
            throw new ConflictException(
                ErrorCodes.DayAlreadyClosed,
                "This production day has already been closed and can no longer be changed.");
        }
    }

    public void Touch(Guid userId, DateTimeOffset now)
    {
        UpdatedBy = userId;
        UpdatedAt = now;
    }
}
