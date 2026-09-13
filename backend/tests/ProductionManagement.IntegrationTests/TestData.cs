using System.Net.Http.Json;

namespace ProductionManagement.IntegrationTests;

/// <summary>Khuôn của các response API mà test dùng để assert.</summary>
public sealed record ApiErrorDetail(string Field, string Code, string Message);

public sealed record ApiErrorResponse(
    string Code, string Message, IReadOnlyList<ApiErrorDetail>? Details);

public sealed record ProductionLineResponse(
    Guid Id, string Code, string Name, string Status, int SortOrder, string? Note, bool InUse);

public sealed record ProductionLineListResponse(IReadOnlyList<ProductionLineResponse> Items);

public sealed record OrderProductionLineResponse(
    Guid Id, string Code, string Name, string Status, int SortOrder, int AllocatedQuantity);

public sealed record OrderResponse(
    Guid Id,
    string ShoeCode,
    int Quantity,
    DateOnly? StartDate,
    DateOnly? DueDate,
    string Status,
    bool HasImage,
    string? ImageUrl,
    IReadOnlyList<OrderProductionLineResponse> ProductionLines,
    int TotalActual,
    int Remaining,
    int TotalPlan,
    int TotalInitialPlan,
    decimal ProgressPercentage,
    bool IsOverdue,
    bool IsPastDueDate);

/// <summary>Một ô của ma trận: ngày × dây chuyền.</summary>
public sealed record ProductionCellResponse(
    Guid Id,
    DateOnly ProductionDate,
    Guid ProductionLineId,
    int InitialPlannedQuantity,
    int AddOnQuantity,
    int PlannedQuantity,
    string DayStatus,
    int? ActualQuantity,
    bool IsProvisional,
    Guid? ProductionDayId,
    int? ShortageQuantity,
    int? Difference,
    DateTimeOffset? ClosedAt,
    bool HasActiveAdjustment,
    Guid? ActiveAdjustmentId);

public sealed record ProductionMatrixLineResponse(
    Guid Id, string Code, string Name, string Status, int SortOrder,
    int AllocatedQuantity, int CurrentPlanQuantity, int ActualQuantity);

public sealed record ProductionMatrixResponse(
    Guid OrderId,
    DateOnly? StartDate,
    DateOnly? DueDate,
    IReadOnlyList<ProductionMatrixLineResponse> ProductionLines,
    IReadOnlyList<ProductionCellResponse> Items);

public sealed record ProductionEntryResponse(
    Guid Id, int Quantity, DateTimeOffset RecordedAt, string? Note, int RunningTotal, bool IsEdited);

public sealed record ProductionCellDetailResponse(
    Guid OrderId,
    DateOnly ProductionDate,
    Guid ProductionLineId,
    string ProductionLineCode,
    string DayStatus,
    int PlannedQuantity,
    int AddOnQuantity,
    int DayActualQuantity,
    bool IsProvisional,
    int RemainingAllowance,
    string RemainingAllowanceReason,
    int OrderRemainingQuantity,
    string OrderStatus,
    DateTimeOffset? LastRecordedAt,
    DateTimeOffset? ClosedAt,
    int? ShortageQuantity,
    int? Difference,
    IReadOnlyList<ProductionEntryResponse> Entries);

public sealed record ClosedProductionCellResponse(
    Guid ProductionLineId,
    string ProductionLineCode,
    int PlannedQuantity,
    int ActualQuantity,
    int ShortageQuantity,
    int Difference);

public sealed record CloseProductionDayResponse(
    DateOnly ProductionDate,
    DateTimeOffset ClosedAt,
    IReadOnlyList<ClosedProductionCellResponse> Cells,
    string OrderStatus,
    bool OrderCompleted,
    bool HasShortage);

public sealed record SystemSettingsResponse(int RecordingIntervalMinutes, bool RemindBeforeDue);

public sealed record AdjustmentPreviewItemResponse(
    Guid ProductionPlanId, DateOnly ProductionDate, Guid ProductionLineId, string ProductionLineName,
    int CurrentPlannedQuantity, int AddOnQuantity, int PlannedQuantityAfter);

public sealed record AdjustmentPreviewResponse(
    Guid SourceProductionPlanId,
    Guid ProductionLineId,
    string ProductionLineCode,
    int ShortageQuantity,
    string AdjustmentType,
    IReadOnlyList<AdjustmentPreviewItemResponse> Items,
    int TotalAddOnQuantity,
    bool Valid,
    string? ValidationCode);

public sealed record PlanAdjustmentItemResponse(Guid ProductionPlanId, DateOnly ProductionDate, int AddOnQuantity);

public sealed record AdjustmentLineResponse(Guid Id, string Code, string Name);

public sealed record PlanAdjustmentResponse(
    Guid Id,
    Guid SourceProductionPlanId,
    AdjustmentLineResponse ProductionLine,
    int ShortageQuantity,
    string AdjustmentType,
    string Status,
    IReadOnlyList<PlanAdjustmentItemResponse> Items,
    string? ReversedBy);

public static class HttpExtensions
{
    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(ApiFactory.Json);
        return value ?? throw new InvalidOperationException($"Response body was empty for {typeof(T).Name}.");
    }

    public static Task<ApiErrorResponse> ReadErrorAsync(this HttpResponseMessage response)
        => response.ReadAsync<ApiErrorResponse>();
}
