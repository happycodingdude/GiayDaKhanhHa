namespace ProductionManagement.Application.Contracts;

public sealed record AdjustmentTargetRequest(long ProductionPlanId, int AddOnQuantity);

/// <summary>
/// Preview request. Manual carries the manager's chosen targets; Automatic asks the backend to
/// calculate the proposal (Step 4 §8). Preview never persists anything.
/// </summary>
public sealed record PreviewAdjustmentRequest(
    string? AdjustmentType,
    IReadOnlyList<AdjustmentTargetRequest>? Targets);

/// <summary>
/// Apply request. The client resubmits the proposal it reviewed, including the shortage it was
/// based on, so the server can detect a stale preview (Step 4 §11).
/// </summary>
public sealed record ApplyAdjustmentRequest(
    string? AdjustmentType,
    int ShortageQuantity,
    IReadOnlyList<AdjustmentTargetRequest>? Targets);

public sealed record AdjustmentPreviewItemDto(
    long ProductionPlanId,
    DateOnly ProductionDate,
    int CurrentPlannedQuantity,
    int AddOnQuantity,
    int PlannedQuantityAfter);

public sealed record AdjustmentPreviewDto(
    long SourceProductionPlanId,
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
    long ProductionPlanId,
    DateOnly ProductionDate,
    int AddOnQuantity);

/// <summary>
/// What happened to a source day's active adjustment when its actual quantity was edited.
/// </summary>
public enum AdjustmentRecalculationOutcome
{
    /// <summary>The shortage changed, so the add-on was redistributed onto the target days.</summary>
    Recalculated,

    /// <summary>The day no longer has a shortage, so the add-on was removed entirely.</summary>
    Removed,

    /// <summary>
    /// The old add-on was removed, but the new shortage could not be placed anywhere because no
    /// eligible target day is left. The shortage is reported back as unhandled.
    /// </summary>
    Unhandled
}

/// <summary>
/// Reported alongside the updated production record so the manager is told what happened to the
/// add-on they had already applied. Absent when the day had no active adjustment, or when the
/// shortage did not change.
/// </summary>
public sealed record AdjustmentRecalculationDto(
    AdjustmentRecalculationOutcome Outcome,
    long ReversedAdjustmentId,
    int PreviousShortageQuantity,
    int ShortageQuantity,
    string AdjustmentType,
    long? AdjustmentId,
    IReadOnlyList<PlanAdjustmentItemDto> Items);

public sealed record PlanAdjustmentDto(
    long Id,
    long SourceProductionPlanId,
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
