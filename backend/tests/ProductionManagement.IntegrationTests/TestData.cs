using System.Net.Http.Json;

namespace ProductionManagement.IntegrationTests;

/// <summary>Khuôn của các response API mà test dùng để assert.</summary>
public sealed record ApiErrorResponse(string Code, string Message);

public sealed record OrderResponse(
    Guid Id,
    string OrderCode,
    int Quantity,
    string Status,
    int TotalActual,
    int Remaining,
    int TotalPlan,
    int TotalInitialPlan,
    decimal ProgressPercentage,
    bool IsOverdue,
    bool IsPastDueDate);

public sealed record ProductionDayResponse(
    Guid Id,
    DateOnly ProductionDate,
    int InitialPlannedQuantity,
    int AddOnQuantity,
    int PlannedQuantity,
    int? ActualQuantity,
    Guid? ProductionRecordId,
    int ShortageQuantity,
    int? Difference,
    bool HasActiveAdjustment,
    Guid? ActiveAdjustmentId);

public sealed record ProductionPlanListResponse(Guid OrderId, IReadOnlyList<ProductionDayResponse> Items);

public sealed record ProductionRecordResponse(
    Guid Id,
    DateOnly ProductionDate,
    int ActualQuantity,
    AdjustmentRecalculationResponse? AdjustmentRecalculation);

public sealed record AdjustmentRecalculationResponse(
    string Outcome,
    Guid ReversedAdjustmentId,
    int PreviousShortageQuantity,
    int ShortageQuantity,
    string AdjustmentType,
    Guid? AdjustmentId,
    IReadOnlyList<PlanAdjustmentItemResponse> Items);

public sealed record AdjustmentPreviewItemResponse(
    Guid ProductionPlanId, DateOnly ProductionDate, int CurrentPlannedQuantity, int AddOnQuantity, int PlannedQuantityAfter);

public sealed record AdjustmentPreviewResponse(
    Guid SourceProductionPlanId,
    int ShortageQuantity,
    string AdjustmentType,
    IReadOnlyList<AdjustmentPreviewItemResponse> Items,
    int TotalAddOnQuantity,
    bool Valid,
    string? ValidationCode);

public sealed record PlanAdjustmentItemResponse(Guid ProductionPlanId, DateOnly ProductionDate, int AddOnQuantity);

public sealed record PlanAdjustmentResponse(
    Guid Id,
    Guid SourceProductionPlanId,
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
