namespace ProductionManagement.Domain.Entities;

/// <summary>
/// Aggregate root for production management (Step 1 §3).
/// Derived values (TotalActual, Remaining, Progress, TotalPlan) are never persisted here.
/// </summary>
public sealed class Order
{
    private readonly List<ProductionPlan> _productionPlans = [];
    private readonly List<ProductionRecord> _productionRecords = [];

    private Order() { }

    public long Id { get; private set; }
    public string OrderCode { get; private set; } = null!;
    public int Quantity { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly DueDate { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<ProductionPlan> ProductionPlans => _productionPlans;
    public IReadOnlyCollection<ProductionRecord> ProductionRecords => _productionRecords;

    public bool IsCompleted => Status == OrderStatus.Completed;

    /// <summary>
    /// Creates the Order together with its initial production plans. Both are persisted in one
    /// transaction (Step 4 §19) and must satisfy SUM(InitialPlannedQuantity) == Order.Quantity.
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

        // SUM(InitialPlannedQuantity) == Order.Quantity is a hard business invariant (Step 3 §12).
        var totalPlanned = initialPlans.Sum(p => (long)p.PlannedQuantity);
        if (totalPlanned != quantity)
        {
            throw new BusinessRuleException(
                ErrorCodes.InitialPlanTotalMismatch,
                $"The total initial production plan ({totalPlanned}) must equal the order quantity ({quantity}).");
        }

        var order = new Order
        {
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
    /// Order status is derived from the total actual quantity and is never set by the manager
    /// (Step 1 §13). Must be called inside the same transaction that changed a production record.
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
