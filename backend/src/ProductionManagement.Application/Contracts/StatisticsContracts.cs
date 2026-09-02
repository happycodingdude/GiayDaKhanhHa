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

/// <summary>Vị thế sản xuất hôm nay của một đơn hàng, dùng cho panel "hôm nay" của dashboard.</summary>
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

/// <summary>
/// Một ngày sản xuất của đơn hàng, rút gọn cho timeline của dashboard: chỉ đủ để biết ngày đó
/// có kế hoạch không và đã đạt kế hoạch chưa. Ngày không có kế hoạch không xuất hiện ở đây.
/// </summary>
public sealed record DashboardOrderDayDto(
    DateOnly ProductionDate,
    int PlannedQuantity,
    int? ActualQuantity);

public sealed record DashboardOrderDto(
    Guid OrderId,
    string OrderCode,
    DateOnly StartDate,
    DateOnly DueDate,
    decimal ProgressPercentage,
    int? TodayDifference,
    bool TodayHasPlan,
    int Remaining,
    ScheduleStatus ScheduleStatus,
    int BehindQuantity,
    IReadOnlyList<DashboardOrderDayDto> Days);

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
