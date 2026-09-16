using ProductionManagement.Domain;

namespace ProductionManagement.Application.Contracts;

/// <summary>
/// Một ảnh do client tải lên. Tầng application chủ đích không biết tới <c>IFormFile</c> của
/// ASP.NET — controller là nơi duy nhất chạm vào kiểu đó.
/// </summary>
public sealed record ImageUpload(string? FileName, string? ContentType, long SizeBytes, Stream Content);

/// <summary>Nhập hàng: chỉ mã giày, số lượng và (tuỳ chọn) một ảnh mẫu (CR-001 §6.4).</summary>
public sealed record CreateOrderRequest(string? ShoeCode, int Quantity);

/// <summary>
/// Sửa thông tin nhập hàng. Ảnh mới (nếu có) đi kèm cùng request dưới dạng file; <see cref="RemoveImage"/>
/// gỡ ảnh hiện tại. Mọi thứ được lưu trong một lần.
/// </summary>
public sealed record UpdateOrderRequest(string? ShoeCode, int Quantity, bool RemoveImage = false);

/// <summary>Một dây chuyền thuộc tiến độ của đơn hàng, kèm số đã phân bổ ở tầng 1.</summary>
public sealed record OrderProductionLineDto(
    Guid Id,
    string Code,
    string Name,
    string Status,
    int SortOrder,
    int AllocatedQuantity);

/// <summary>
/// Dòng đơn hàng cho màn hình danh sách. Mọi số lượng tổng hợp đều là suy ra, không lưu xuống.
/// </summary>
public sealed record OrderListItemDto(
    Guid Id,
    string ShoeCode,
    int Quantity,

    /// <summary>Đơn chưa lập tiến độ chưa có ngày (CR-001 BR-N04).</summary>
    DateOnly? StartDate,
    DateOnly? DueDate,
    string Status,
    bool HasImage,

    /// <summary>Endpoint có xác thực; đường dẫn vật lý không bao giờ ra ngoài (CR-001 §2 QĐ-6).</summary>
    string? ImageUrl,
    IReadOnlyList<OrderProductionLineDto> ProductionLines,
    int TotalActual,
    int Remaining,
    int TotalPlan,
    decimal ProgressPercentage,
    ScheduleStatus ScheduleStatus,
    int BehindQuantity,
    int DaysRemaining,
    bool IsOverdue,

    /// <summary>
    /// Vị thế của hôm nay, gộp mọi dây chuyền, để danh sách trả lời được "hôm nay đơn nào đang chạy
    /// tới đâu" mà không phải mở từng đơn (CR-01 §8, MH1). Null khi hôm nay không có kế hoạch.
    /// </summary>
    int? TodayPlannedQuantity,
    int? TodayActualQuantity,

    /// <summary>Có ô đã qua chưa Xuất hàng — chỉ báo việc bị treo (CR-01 §14.5).</summary>
    bool HasUnclosedPastCell,

    /// <summary>
    /// Số ngày đã qua còn chưa Xuất hàng. Đếm theo ngày, không theo ô: Xuất hàng chốt sổ cả ngày một
    /// lượt. Luôn bằng 0 với đơn không còn ở trạng thái Chưa hoàn thành, giống <see cref="HasUnclosedPastCell"/>.
    /// </summary>
    int UnclosedPastDayCount);

public sealed record OrderDetailDto(
    Guid Id,
    string ShoeCode,
    int Quantity,
    DateOnly? StartDate,
    DateOnly? DueDate,
    string Status,
    bool HasImage,
    string? ImageUrl,
    string? ImageFileName,
    IReadOnlyList<OrderProductionLineDto> ProductionLines,
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

// --- Lập tiến độ (CR-001 §6.6) -------------------------------------------------------------

public sealed record SchedulePlanRequest(DateOnly ProductionDate, int PlannedQuantity);

public sealed record ScheduleLineRequest(
    Guid ProductionLineId,
    int AllocatedQuantity,
    IReadOnlyList<SchedulePlanRequest>? Plans);

/// <summary>
/// Request lồng theo dây chuyền để phản ánh đúng cấu trúc phân bổ 2 tầng.
/// <see cref="AllocationMode"/> chỉ mang tính khai báo cho audit; backend LUÔN validate con số thật
/// trong <see cref="Lines"/>, không tự tính lại theo mode (CR-001 §6.6, BR-N16).
/// </summary>
public sealed record CreateProductionScheduleRequest(
    DateOnly StartDate,
    DateOnly DueDate,
    string? AllocationMode,
    IReadOnlyList<ScheduleLineRequest>? Lines);
