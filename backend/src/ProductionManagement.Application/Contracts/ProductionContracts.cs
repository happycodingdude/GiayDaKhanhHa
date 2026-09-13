using ProductionManagement.Domain;

namespace ProductionManagement.Application.Contracts;

/// <summary>
/// Một ô của ma trận sản xuất — một ngày trên một dây chuyền: kế hoạch, sản lượng và các giá trị
/// suy ra, ghép sẵn để frontend không phải gọi nhiều API rồi tự join (CR-001 §6.7).
///
/// Backend trả phẳng theo ô; frontend dựng ma trận. Ô không có kế hoạch đơn giản là không xuất hiện
/// trong danh sách (CR-001 §6.6b).
/// </summary>
public sealed record ProductionCellDto(
    /// <summary>Id của ProductionPlan — khoá của ô, và là id mà luồng bù sản lượng dùng.</summary>
    Guid Id,
    DateOnly ProductionDate,
    Guid ProductionLineId,
    int InitialPlannedQuantity,
    int AddOnQuantity,
    int PlannedQuantity,

    /// <summary>Trạng thái hiển thị do server suy ra; frontend không bao giờ tự tính (CR-01 §14.3).</summary>
    ProductionDayDisplayStatus DayStatus,

    /// <summary>
    /// Tổng các lần ghi nhận chưa xoá của ô. Null nghĩa là chưa ghi nhận lần nào, khác hẳn với 0.
    /// Khi <see cref="IsProvisional"/> là true thì đây là số tạm tính, chưa chốt sổ.
    /// </summary>
    int? ActualQuantity,
    bool IsProvisional,
    Guid? ProductionDayId,

    /// <summary>Chỉ có giá trị khi ô đã Xuất hàng; ô còn mở trả null (CR-01 OV-5).</summary>
    int? ShortageQuantity,
    int? Difference,
    DateTimeOffset? ClosedAt,

    // True khi ô này là nguồn của một điều chỉnh đang ở trạng thái Applied.
    bool HasActiveAdjustment,
    Guid? ActiveAdjustmentId,
    string? LastRecordedBy,
    DateTimeOffset? LastRecordedAt);

/// <summary>Một dây chuyền trên ma trận, kèm tổng hợp theo cột.</summary>
public sealed record ProductionMatrixLineDto(
    Guid Id,
    string Code,
    string Name,
    string Status,
    int SortOrder,

    /// <summary>Mốc phân bổ tầng 1, bất biến sau khi lập tiến độ (BR-N18).</summary>
    int AllocatedQuantity,

    /// <summary>Kế hoạch hiện tại của cả dây chuyền — có thể lớn hơn <c>AllocatedQuantity</c> sau bù.</summary>
    int CurrentPlanQuantity,
    int ActualQuantity);

/// <summary>Ma trận ngày × dây chuyền của một đơn hàng (CR-001 §6.7).</summary>
public sealed record ProductionMatrixDto(
    Guid OrderId,
    DateOnly? StartDate,
    DateOnly? DueDate,
    IReadOnlyList<ProductionMatrixLineDto> ProductionLines,
    IReadOnlyList<ProductionCellDto> Items);

/// <summary>Một lần ghi nhận sản lượng, kèm tổng lũy kế do server tính (CR-01 §6.3).</summary>
public sealed record ProductionEntryDto(
    Guid Id,
    int Quantity,
    DateTimeOffset RecordedAt,
    string? Note,
    int RunningTotal,
    bool IsEdited,
    string? RecordedBy);

/// <summary>
/// Toàn bộ state của một ô sản xuất — màn hình chính của luồng ghi nhận (CR-01 §6.3).
/// POST/PUT/DELETE entry cũng trả về đúng khuôn này để frontend không phải refetch thêm một vòng.
/// </summary>
public sealed record ProductionCellDetailDto(
    Guid OrderId,
    string ShoeCode,
    DateOnly ProductionDate,
    Guid ProductionLineId,
    string ProductionLineCode,
    string ProductionLineName,
    ProductionDayDisplayStatus DayStatus,
    int InitialPlannedQuantity,
    int PlannedQuantity,
    int AddOnQuantity,
    int DayActualQuantity,
    bool IsProvisional,

    /// <summary>Số hiển thị trên ô "Còn được nhập" = MIN(trần ô, trần đơn hàng).</summary>
    int RemainingAllowance,

    /// <summary>Ràng buộc nào đang chặn, để UI chọn đúng câu thông báo.</summary>
    RemainingAllowanceReason RemainingAllowanceReason,
    int OrderRemainingQuantity,
    string OrderStatus,
    bool IsOrderReadOnly,
    DateTimeOffset? LastRecordedAt,
    DateTimeOffset? ClosedAt,
    string? ClosedBy,
    int? ShortageQuantity,
    int? Difference,
    IReadOnlyList<ProductionEntryDto> Entries);

public sealed record CreateProductionEntryRequest(int Quantity, string? Note);

public sealed record UpdateProductionEntryRequest(int Quantity, string? Note);

/// <summary>Một dây chuyền vừa được chốt sổ trong lần Xuất hàng cả ngày.</summary>
public sealed record ClosedProductionCellDto(
    Guid ProductionLineId,
    string ProductionLineCode,
    string ProductionLineName,
    int PlannedQuantity,
    int ActualQuantity,
    int ShortageQuantity,
    int Difference);

/// <summary>
/// Kết quả Xuất hàng một ngày: mọi dây chuyền có kế hoạch mà còn mở trong ngày đó được chốt sổ
/// cùng lúc. <c>HasShortage</c> là tín hiệu để frontend báo có phần thiếu cần xử lý (CR-01 §6.6).
/// </summary>
public sealed record CloseProductionDayDto(
    Guid OrderId,
    DateOnly ProductionDate,
    DateTimeOffset ClosedAt,
    IReadOnlyList<ClosedProductionCellDto> Cells,
    string OrderStatus,
    bool OrderCompleted,
    bool HasShortage);

public sealed record SystemSettingsDto(
    int RecordingIntervalMinutes,
    bool RemindBeforeDue,
    DateTimeOffset UpdatedAt);

public sealed record UpdateSystemSettingsRequest(
    int RecordingIntervalMinutes,
    bool RemindBeforeDue);
