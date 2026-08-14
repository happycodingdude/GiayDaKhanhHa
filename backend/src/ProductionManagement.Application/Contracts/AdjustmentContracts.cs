namespace ProductionManagement.Application.Contracts;

public sealed record AdjustmentTargetRequest(Guid ProductionPlanId, int AddOnQuantity);

/// <summary>
/// Request xem trước. Manual mang theo các ngày đích do quản lý chọn; Automatic nhờ backend tự tính
/// đề xuất (Step 4 §8). Preview không bao giờ lưu gì xuống.
/// </summary>
public sealed record PreviewAdjustmentRequest(
    string? AdjustmentType,
    IReadOnlyList<AdjustmentTargetRequest>? Targets);

/// <summary>
/// Request áp dụng. Client gửi lại đúng đề xuất đã xem, kèm phần thiếu mà nó dựa vào, để server
/// phát hiện được preview đã cũ (Step 4 §11).
/// </summary>
public sealed record ApplyAdjustmentRequest(
    string? AdjustmentType,
    int ShortageQuantity,
    IReadOnlyList<AdjustmentTargetRequest>? Targets);

public sealed record AdjustmentPreviewItemDto(
    Guid ProductionPlanId,
    DateOnly ProductionDate,
    int CurrentPlannedQuantity,
    int AddOnQuantity,
    int PlannedQuantityAfter);

public sealed record AdjustmentPreviewDto(
    Guid SourceProductionPlanId,
    DateOnly SourceProductionDate,
    int SourcePlannedQuantity,
    int? SourceActualQuantity,
    int ShortageQuantity,
    string AdjustmentType,
    IReadOnlyList<AdjustmentPreviewItemDto> Items,
    int TotalAddOnQuantity,
    bool Valid,
    string? ValidationCode,
    string? ValidationMessage);

public sealed record PlanAdjustmentItemDto(
    Guid ProductionPlanId,
    DateOnly ProductionDate,
    int AddOnQuantity);

/// <summary>
/// Điều gì đã xảy ra với điều chỉnh đang hiệu lực của ngày nguồn khi sản lượng thực tế bị sửa.
/// </summary>
public enum AdjustmentRecalculationOutcome
{
    /// <summary>Phần thiếu thay đổi nên khoản bù được chia lại lên các ngày đích.</summary>
    Recalculated,

    /// <summary>Ngày đó không còn thiếu nữa nên khoản bù bị gỡ bỏ hoàn toàn.</summary>
    Removed,

    /// <summary>
    /// Khoản bù cũ đã bị gỡ, nhưng phần thiếu mới không đặt được vào đâu vì không còn ngày đích nào
    /// hợp lệ. Phần thiếu được báo ngược về là chưa xử lý.
    /// </summary>
    Unhandled
}

/// <summary>
/// Được trả kèm bản ghi sản xuất vừa cập nhật để quản lý biết khoản bù họ đã áp dụng giờ ra sao.
/// Không có khi ngày đó không có điều chỉnh đang hiệu lực, hoặc khi phần thiếu không đổi.
/// </summary>
public sealed record AdjustmentRecalculationDto(
    AdjustmentRecalculationOutcome Outcome,
    Guid ReversedAdjustmentId,
    int PreviousShortageQuantity,
    int ShortageQuantity,
    string AdjustmentType,
    Guid? AdjustmentId,
    IReadOnlyList<PlanAdjustmentItemDto> Items);

public sealed record PlanAdjustmentDto(
    Guid Id,
    Guid SourceProductionPlanId,
    DateOnly SourceProductionDate,
    int ShortageQuantity,
    string AdjustmentType,
    string Status,
    IReadOnlyList<PlanAdjustmentItemDto> Items,
    string CreatedBy,
    string? AppliedBy,
    string? ReversedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? AppliedAt,
    DateTimeOffset? ReversedAt);
