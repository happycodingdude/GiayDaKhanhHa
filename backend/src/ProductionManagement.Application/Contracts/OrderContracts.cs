namespace ProductionManagement.Application.Contracts;

public sealed record CreateProductionPlanRequest(DateOnly ProductionDate, int PlannedQuantity);

/// <summary>Tạo đơn hàng luôn kèm theo kế hoạch sản xuất ban đầu (Step 4 §5).</summary>
public sealed record CreateOrderRequest(
    string? OrderCode,
    int Quantity,
    DateOnly StartDate,
    DateOnly DueDate,
    IReadOnlyList<CreateProductionPlanRequest>? ProductionPlans);

/// <summary>
/// Dòng đơn hàng cho màn hình danh sách. Mọi số lượng tổng hợp đều là suy ra, không lưu xuống.
/// </summary>
public sealed record OrderListItemDto(
    Guid Id,
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
    Guid Id,
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
    /// <summary>Kỳ sản xuất đã kết thúc nên đơn hàng chỉ đọc. Đúng với cả đơn đã hoàn thành.</summary>
    bool IsPastDueDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
