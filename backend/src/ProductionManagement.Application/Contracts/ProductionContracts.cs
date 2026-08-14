namespace ProductionManagement.Application.Contracts;

/// <summary>
/// Một ngày sản xuất, gộp kế hoạch, bản ghi thực tế và các giá trị suy ra để frontend không phải
/// ghép nhiều API mới dựng được bảng theo ngày (Step 4 §6).
/// </summary>
public sealed record ProductionDayDto(
    Guid Id,
    DateOnly ProductionDate,
    int InitialPlannedQuantity,
    int AddOnQuantity,
    int PlannedQuantity,

    // Null nghĩa là chưa nhập thực tế, khác hẳn với giá trị 0.
    int? ActualQuantity,
    Guid? ProductionRecordId,
    int ShortageQuantity,
    int? Difference,

    // True khi ngày này là ngày nguồn của một điều chỉnh đang ở trạng thái Applied.
    bool HasActiveAdjustment,
    Guid? ActiveAdjustmentId,
    string? ActualEnteredBy,
    DateTimeOffset? ActualUpdatedAt);

public sealed record ProductionPlanListDto(Guid OrderId, IReadOnlyList<ProductionDayDto> Items);

public sealed record CreateProductionRecordRequest(DateOnly ProductionDate, int ActualQuantity);

/// <summary>
/// Thực tế là một giá trị, không phải số cộng thêm: thao tác này thay thế giá trị đã lưu (Step 4 §7).
/// </summary>
public sealed record UpdateProductionRecordRequest(int ActualQuantity);

public sealed record ProductionRecordDto(
    Guid Id,
    Guid OrderId,
    DateOnly ProductionDate,
    int ActualQuantity,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,

    /// <summary>
    /// Được set khi việc sửa thực tế này làm thay đổi phần thiếu mà điều chỉnh đang hiệu lực của ngày
    /// dựa vào, khiến khoản bù phải dựng lại. Luôn null khi tạo mới bản ghi: một ngày không thể có
    /// điều chỉnh trước khi có thực tế.
    /// </summary>
    AdjustmentRecalculationDto? AdjustmentRecalculation = null);
