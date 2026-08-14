namespace ProductionManagement.Application.Contracts;

/// <summary>
/// One production day, combining the plan, the actual record and the derived values so the
/// frontend never has to join several APIs to build the daily view (Step 4 §6).
/// </summary>
public sealed record ProductionDayDto(
    Guid Id,
    DateOnly ProductionDate,
    int InitialPlannedQuantity,
    int AddOnQuantity,
    int PlannedQuantity,

    // Null means the actual has not been entered yet, which is distinct from 0.
    int? ActualQuantity,
    Guid? ProductionRecordId,
    int ShortageQuantity,
    int? Difference,

    // True when this day is the source of an adjustment that is currently Applied.
    bool HasActiveAdjustment,
    Guid? ActiveAdjustmentId,
    string? ActualEnteredBy,
    DateTimeOffset? ActualUpdatedAt);

public sealed record ProductionPlanListDto(Guid OrderId, IReadOnlyList<ProductionDayDto> Items);

public sealed record CreateProductionRecordRequest(DateOnly ProductionDate, int ActualQuantity);

/// <summary>
/// Actual is a value, not an increment: this replaces the stored value (Step 4 §7).
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
    /// Set when editing this actual changed the shortage the day's active adjustment was based on,
    /// so the add-on had to be rebuilt. Always null when creating a record: a day cannot have an
    /// adjustment before it has an actual.
    /// </summary>
    AdjustmentRecalculationDto? AdjustmentRecalculation = null);
