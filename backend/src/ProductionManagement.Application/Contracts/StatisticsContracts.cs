namespace ProductionManagement.Application.Contracts;

public sealed record DailyStatisticsDto(
    DateOnly ProductionDate,
    int InitialPlannedQuantity,
    int AddOnQuantity,
    int PlannedQuantity,
    int? ActualQuantity,
    int? Difference,
    int ShortageQuantity,
    int CumulativePlan,
    int CumulativeActual);

public sealed record OrderStatisticsDto(
    Guid OrderId,
    string OrderCode,
    int OrderQuantity,
    int TotalActual,
    int Remaining,
    int TotalPlan,
    int TotalInitialPlan,
    decimal ProgressPercentage,
    ScheduleStatus ScheduleStatus,
    int BehindQuantity,
    int DaysRemaining,
    bool IsOverdue,
    IReadOnlyList<DailyStatisticsDto> Daily);

/// <summary>Today's production position for one order, used by the dashboard "today" panel.</summary>
public sealed record DashboardTodayDto(
    int PlannedQuantity,
    int ActualQuantity,
    bool HasAnyActualEntered,
    int Difference,
    decimal CompletionPercentage);

public sealed record DashboardAlertDto(
    Guid OrderId,
    string OrderCode,
    int BehindQuantity,
    int DaysRemaining,
    bool IsOverdue,
    DateOnly DueDate);

public sealed record DashboardOrderDto(
    Guid OrderId,
    string OrderCode,
    decimal ProgressPercentage,
    int? TodayDifference,
    bool TodayHasPlan,
    int Remaining,
    ScheduleStatus ScheduleStatus,
    int BehindQuantity);

public sealed record DashboardStatisticsDto(
    DateOnly Date,
    int TotalOrders,
    int IncompleteOrders,
    int CompletedOrders,
    int BehindOrders,
    int TotalOrderQuantity,
    int TotalActualQuantity,
    int TotalRemainingQuantity,
    DashboardTodayDto Today,
    IReadOnlyList<DashboardAlertDto> Alerts,
    IReadOnlyList<DashboardOrderDto> TrackedOrders);
