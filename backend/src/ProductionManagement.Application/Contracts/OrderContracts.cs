namespace ProductionManagement.Application.Contracts;

public sealed record CreateProductionPlanRequest(DateOnly ProductionDate, int PlannedQuantity);

/// <summary>Order creation always includes the initial production plans (Step 4 §5).</summary>
public sealed record CreateOrderRequest(
    string? OrderCode,
    int Quantity,
    DateOnly StartDate,
    DateOnly DueDate,
    IReadOnlyList<CreateProductionPlanRequest>? ProductionPlans);

/// <summary>
/// Order row for the list screen. All quantity aggregates are derived, never persisted.
/// </summary>
public sealed record OrderListItemDto(
    long Id,
    string OrderCode,
    int Quantity,
    DateOnly StartDate,
    DateOnly DueDate,
    string Status,
    int TotalActual,
    int Remaining,
    int TotalPlan,
    decimal ProgressPercentage,
    ScheduleStatus ScheduleStatus,
    int BehindQuantity,
    int DaysRemaining,
    bool IsOverdue);

public sealed record OrderDetailDto(
    long Id,
    string OrderCode,
    int Quantity,
    DateOnly StartDate,
    DateOnly DueDate,
    string Status,
    int TotalActual,
    int Remaining,
    int TotalPlan,
    int TotalInitialPlan,
    decimal ProgressPercentage,
    ScheduleStatus ScheduleStatus,
    int BehindQuantity,
    int DaysRemaining,
    bool IsOverdue,
    /// <summary>The production period is over, so the order is read-only. True for completed orders too.</summary>
    bool IsPastDueDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
