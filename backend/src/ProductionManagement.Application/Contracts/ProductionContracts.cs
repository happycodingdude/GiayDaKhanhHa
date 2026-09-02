using ProductionManagement.Domain;

namespace ProductionManagement.Application.Contracts;

/// <summary>
/// Một dòng của bảng sản xuất theo ngày ở màn hình chi tiết đơn hàng: kế hoạch, sản lượng và các
/// giá trị suy ra, ghép sẵn để frontend không phải gọi nhiều API (Step 4 §6).
/// </summary>
public sealed record ProductionDayDto(
    Guid Id,
    DateOnly ProductionDate,
    int InitialPlannedQuantity,
    int AddOnQuantity,
    int PlannedQuantity,

    /// <summary>Trạng thái hiển thị do server suy ra; frontend không bao giờ tự tính (CR-01 §14.3).</summary>
    ProductionDayDisplayStatus DayStatus,

    /// <summary>
    /// Tổng các lần ghi nhận chưa xoá. Null nghĩa là chưa ghi nhận lần nào, khác hẳn với 0.
    /// Khi <see cref="IsProvisional"/> là true thì đây là số tạm tính, chưa chốt sổ.
    /// </summary>
    int? ActualQuantity,
    bool IsProvisional,
    Guid? ProductionDayId,

    /// <summary>Chỉ có giá trị khi ngày đã Xuất hàng; ngày còn mở trả null (CR-01 OV-5).</summary>
    int? ShortageQuantity,
    int? Difference,
    DateTimeOffset? ClosedAt,

    // True khi ngày này là ngày nguồn của một điều chỉnh đang ở trạng thái Applied.
    bool HasActiveAdjustment,
    Guid? ActiveAdjustmentId,
    string? LastRecordedBy,
    DateTimeOffset? LastRecordedAt);

public sealed record ProductionPlanListDto(Guid OrderId, IReadOnlyList<ProductionDayDto> Items);

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
/// Toàn bộ state của một ngày sản xuất — màn hình chính của luồng ghi nhận (CR-01 §6.3).
/// POST/PUT/DELETE entry cũng trả về đúng khuôn này để frontend không phải refetch thêm một vòng.
/// </summary>
public sealed record ProductionDayDetailDto(
    Guid OrderId,
    string OrderCode,
    DateOnly ProductionDate,
    ProductionDayDisplayStatus DayStatus,
    int InitialPlannedQuantity,
    int PlannedQuantity,
    int AddOnQuantity,
    int DayActualQuantity,
    bool IsProvisional,

    /// <summary>Số hiển thị trên ô "Còn được nhập" = MIN(trần ngày, trần đơn hàng).</summary>
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

/// <summary>
/// Kết quả Xuất hàng. <c>HasShortage</c> là tín hiệu để frontend mở luồng Xử lý thiếu ngay sau khi
/// đóng ngày (CR-01 §6.6).
/// </summary>
public sealed record CloseProductionDayDto(
    Guid OrderId,
    DateOnly ProductionDate,
    ProductionDayDisplayStatus DayStatus,
    int PlannedQuantity,
    int ActualQuantity,
    int ShortageQuantity,
    int Difference,
    DateTimeOffset ClosedAt,
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
