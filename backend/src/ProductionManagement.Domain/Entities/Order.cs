namespace ProductionManagement.Domain.Entities;

/// <summary>
/// Aggregate root của quản lý sản xuất (Step 1 §3).
/// Các giá trị suy ra (TotalActual, Remaining, Progress, TotalPlan) không bao giờ được lưu ở đây.
/// </summary>
public sealed class Order
{
    private readonly List<ProductionPlan> _productionPlans = [];
    private readonly List<ProductionDay> _productionDays = [];

    private Order() { }

    public Guid Id { get; private set; }
    public string OrderCode { get; private set; } = null!;
    public int Quantity { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly DueDate { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<ProductionPlan> ProductionPlans => _productionPlans;
    public IReadOnlyCollection<ProductionDay> ProductionDays => _productionDays;

    public bool IsCompleted => Status == OrderStatus.Completed;

    /// <summary>
    /// Đơn hàng đã qua ngày hạn mà chưa hoàn thành. Đây là cờ "trễ" hiển thị cho quản lý — đơn đã
    /// giao đủ thì không trễ, dù ngày hạn đã qua bao lâu đi nữa.
    /// Suy ra từ ngày hạn và trạng thái, không bao giờ được lưu xuống.
    /// </summary>
    public static bool IsOverdue(OrderStatus status, DateOnly dueDate, DateOnly today)
        => status != OrderStatus.Completed && dueDate < today;

    /// <inheritdoc cref="IsOverdue(OrderStatus, DateOnly, DateOnly)"/>
    public bool IsOverdueOn(DateOnly today) => IsOverdue(Status, DueDate, today);

    /// <summary>
    /// Kỳ sản xuất đã kết thúc. Chủ đích độc lập với trạng thái: đơn đã giao đủ thì vẫn là đã qua
    /// ngày hạn, và vẫn bị đóng băng y như vậy. Bản thân ngày hạn vẫn tính là nằm trong kỳ — thực
    /// tế của ngày đó được nhập vào cuối ngày.
    /// </summary>
    public static bool IsPastDueDate(DateOnly dueDate, DateOnly today) => dueDate < today;

    /// <inheritdoc cref="IsPastDueDate(DateOnly, DateOnly)"/>
    public bool IsPastDueDateOn(DateOnly today) => IsPastDueDate(DueDate, today);

    /// <summary>
    /// Tạo Order cùng các kế hoạch sản xuất ban đầu. Cả hai được lưu trong một transaction
    /// (Step 4 §19) và phải thỏa mãn SUM(InitialPlannedQuantity) == Order.Quantity.
    /// </summary>
    public static Order Create(
        string orderCode,
        int quantity,
        DateOnly startDate,
        DateOnly dueDate,
        IReadOnlyList<(DateOnly ProductionDate, int PlannedQuantity)> initialPlans,
        DateTimeOffset now)
    {
        var failures = new List<ValidationFailure>();

        orderCode = orderCode?.Trim() ?? string.Empty;
        if (orderCode.Length == 0)
        {
            failures.Add(new ValidationFailure("orderCode", "REQUIRED", "Order code is required."));
        }
        else if (orderCode.Length > 50)
        {
            failures.Add(new ValidationFailure("orderCode", "MAX_LENGTH_EXCEEDED", "Order code must be at most 50 characters."));
        }

        if (quantity <= 0)
        {
            failures.Add(new ValidationFailure("quantity", "MUST_BE_GREATER_THAN_ZERO", "Quantity must be greater than zero."));
        }

        if (startDate > dueDate)
        {
            failures.Add(new ValidationFailure("dueDate", "DUE_DATE_BEFORE_START_DATE", "Due date must be on or after the start date."));
        }

        if (initialPlans.Count == 0)
        {
            failures.Add(new ValidationFailure("productionPlans", "REQUIRED", "At least one production plan is required."));
        }

        var seenDates = new HashSet<DateOnly>();
        for (var i = 0; i < initialPlans.Count; i++)
        {
            var (date, planned) = initialPlans[i];

            if (planned < 0)
            {
                failures.Add(new ValidationFailure(
                    $"productionPlans[{i}].plannedQuantity", "MUST_BE_GREATER_THAN_OR_EQUAL_TO_ZERO",
                    "Planned quantity cannot be negative."));
            }

            if (date < startDate || date > dueDate)
            {
                failures.Add(new ValidationFailure(
                    $"productionPlans[{i}].productionDate", "OUT_OF_PRODUCTION_PERIOD",
                    "Production date must fall inside the production period."));
            }

            if (!seenDates.Add(date))
            {
                failures.Add(new ValidationFailure(
                    $"productionPlans[{i}].productionDate", "DUPLICATE_PRODUCTION_DATE",
                    "Each production date can only appear once."));
            }
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        // SUM(InitialPlannedQuantity) == Order.Quantity là bất biến nghiệp vụ cứng (Step 3 §12).
        var totalPlanned = initialPlans.Sum(p => (long)p.PlannedQuantity);
        if (totalPlanned != quantity)
        {
            throw new BusinessRuleException(
                ErrorCodes.InitialPlanTotalMismatch,
                $"The total initial production plan ({totalPlanned}) must equal the order quantity ({quantity}).");
        }

        var order = new Order
        {
            Id = Guid.CreateVersion7(),
            OrderCode = orderCode,
            Quantity = quantity,
            StartDate = startDate,
            DueDate = dueDate,
            Status = OrderStatus.Incomplete,
            CreatedAt = now,
            UpdatedAt = now
        };

        foreach (var (date, planned) in initialPlans.OrderBy(p => p.ProductionDate))
        {
            order._productionPlans.Add(ProductionPlan.Create(order, date, planned, now));
        }

        return order;
    }

    /// <summary>
    /// Trạng thái đơn hàng suy ra từ tổng sản lượng thực tế và không bao giờ do quản lý đặt
    /// (Step 1 §13).
    ///
    /// Chỉ được đánh giá tại đúng một thời điểm: khi Xuất hàng một ngày sản xuất (CR-01 OV-4, §14.1).
    /// Ghi nhận sản lượng trong ngày không đụng tới trạng thái đơn — tổng thực tế bằng số lượng đơn
    /// mà chưa Xuất hàng thì đơn vẫn Incomplete. Phải gọi bên trong đúng transaction đóng ngày.
    /// </summary>
    public void RecalculateStatus(int totalActual, DateTimeOffset now)
    {
        var newStatus = totalActual >= Quantity ? OrderStatus.Completed : OrderStatus.Incomplete;
        if (newStatus == Status)
        {
            return;
        }

        Status = newStatus;
        UpdatedAt = now;
    }
}
