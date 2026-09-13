using ProductionManagement.Domain;

namespace ProductionManagement.Application.Contracts;

/// <summary>
/// Một ngày sản xuất của đơn hàng, GỘP mọi dây chuyền. Đây là đường xu hướng theo thời gian; phần
/// tách theo dây chuyền nằm ở <see cref="OrderLineStatisticsDto"/> (CR-001 §6.9).
/// </summary>
public sealed record DailyStatisticsDto(
    DateOnly ProductionDate,
    int InitialPlannedQuantity,
    int AddOnQuantity,
    int PlannedQuantity,
    int? ActualQuantity,
    ProductionDayDisplayStatus DayStatus,

    /// <summary>Còn ô chưa Xuất hàng: sản lượng của ngày là số tạm tính và còn tăng tiếp (CR-01 §6.9).</summary>
    bool IsProvisional,
    DateTimeOffset? ClosedAt,

    /// <summary>
    /// Chỉ tính trên các ô ĐÃ Xuất hàng của ngày đó. Null khi chưa ô nào đóng — KHÔNG phải 0
    /// (CR-01 N-07, §14.8).
    /// </summary>
    int? Difference,
    int? ShortageQuantity,
    int CumulativePlan,
    int CumulativeActual);

/// <summary>Tách theo dây chuyền cho một đơn hàng (CR-001 §6.9).</summary>
public sealed record OrderLineStatisticsDto(
    Guid ProductionLineId,
    string ProductionLineCode,
    string ProductionLineName,

    /// <summary>Mốc phân bổ tầng 1, bất biến (BR-N18).</summary>
    int AllocatedQuantity,
    int TotalPlan,
    int TotalActual,
    int Shortage);

public sealed record OrderStatisticsDto(
    Guid OrderId,
    string ShoeCode,
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
    IReadOnlyList<DailyStatisticsDto> Daily,
    IReadOnlyList<OrderLineStatisticsDto> ByProductionLine);

/// <summary>
/// Vị thế sản xuất hôm nay của toàn hệ thống. Sản lượng ở đây bao gồm cả số tạm tính của những ô
/// còn đang mở, nên frontend phải gắn nhãn phù hợp (CR-01 §6.9).
/// </summary>
public sealed record DashboardTodayDto(
    int PlannedQuantity,
    int ActualQuantity,
    bool HasAnyActualEntered,
    int Difference,
    decimal CompletionPercentage);

public sealed record DashboardAlertDto(
    Guid OrderId,
    string ShoeCode,
    int BehindQuantity,
    int DaysRemaining,
    bool IsOverdue,
    DateOnly DueDate);

/// <summary>
/// Một ngày sản xuất của đơn hàng, rút gọn cho timeline của dashboard: chỉ đủ để biết ngày đó có kế
/// hoạch không và đã đạt kế hoạch chưa. Gộp mọi dây chuyền. Ngày không có kế hoạch không xuất hiện.
/// </summary>
public sealed record DashboardOrderDayDto(
    DateOnly ProductionDate,
    int PlannedQuantity,
    int? ActualQuantity,
    ProductionDayDisplayStatus DayStatus);

public sealed record DashboardOrderDto(
    Guid OrderId,
    string ShoeCode,
    DateOnly StartDate,
    DateOnly DueDate,
    decimal ProgressPercentage,

    /// <summary>Chỉ có giá trị khi mọi ô của hôm nay đã Xuất hàng — chưa thì chưa có số chính thức.</summary>
    int? TodayDifference,
    bool TodayHasPlan,

    /// <summary>
    /// Sản lượng hôm nay kèm trạng thái, để dashboard hiển thị được cả ngày đang sản xuất chứ không
    /// chỉ ngày đã chốt sổ. Còn ô mở thì đây là số tạm tính (CR-01 §6.9).
    /// </summary>
    int TodayPlannedQuantity,
    int TodayActualQuantity,
    ProductionDayDisplayStatus? TodayStatus,
    int Remaining,
    ScheduleStatus ScheduleStatus,
    int BehindQuantity,
    IReadOnlyList<DashboardOrderDayDto> Days);

/// <summary>Một ô đang sản xuất hôm nay, cho khối "Đang sản xuất hôm nay" (CR-01 §6.9).</summary>
public sealed record DashboardTodayProductionDto(
    Guid OrderId,
    string ShoeCode,
    DateOnly ProductionDate,
    Guid ProductionLineId,
    string ProductionLineCode,
    int PlannedQuantity,
    int DayActualQuantity,
    DateTimeOffset? LastRecordedAt);

/// <summary>
/// Một ô đã qua mà chưa Xuất hàng. Nguồn dữ liệu là <c>production_plans</c>, không phải
/// <c>production_days</c>: ô quá khứ có kế hoạch mà hoàn toàn chưa nhập gì thì chưa có dòng
/// production_days nào, mà đó lại đúng là trường hợp cần cảnh báo nhất (CR-01 §14.5).
/// </summary>
public sealed record DashboardUnclosedDayDto(
    Guid OrderId,
    string ShoeCode,
    DateOnly ProductionDate,
    Guid ProductionLineId,
    string ProductionLineCode,
    int PlannedQuantity,
    int DayActualQuantity);

/// <summary>Phần thiếu của một ô đã Xuất hàng mà chưa được xử lý bù.</summary>
public sealed record DashboardOpenShortageDto(
    Guid OrderId,
    string ShoeCode,
    Guid ProductionPlanId,
    DateOnly ProductionDate,
    Guid ProductionLineId,
    string ProductionLineCode,
    int ShortageQuantity);

public sealed record DashboardStatisticsDto(
    DateOnly Date,
    int TotalOrders,

    /// <summary>Đơn đã nhập hàng nhưng chưa lập tiến độ. Không tham gia tính tiến độ (CR-001 §6.9).</summary>
    int PendingOrderCount,
    int IncompleteOrders,
    int CompletedOrders,
    int BehindOrders,
    int TotalOrderQuantity,
    /// <summary>Bao gồm cả sản lượng tạm tính của các ô đang mở (CR-01 §6.9).</summary>
    int TotalActualQuantity,
    int TotalRemainingQuantity,
    DashboardTodayDto Today,
    IReadOnlyList<DashboardAlertDto> Alerts,
    IReadOnlyList<DashboardOrderDto> TrackedOrders,
    IReadOnlyList<DashboardTodayProductionDto> TodayProduction,
    IReadOnlyList<DashboardUnclosedDayDto> UnclosedPastCells,
    IReadOnlyList<DashboardOpenShortageDto> OpenShortages);
