using System.Net.Http.Json;

namespace ProductionManagement.IntegrationTests;

/// <summary>Shapes of the API responses the tests assert on.</summary>
public sealed record ApiErrorResponse(string Code, string Message);

public sealed record OrderResponse(
    long Id,
    string OrderCode,
    int Quantity,
    string Status,
    int TotalActual,
    int Remaining,
    int TotalPlan,
    int TotalInitialPlan,
    decimal ProgressPercentage);

public sealed record ProductionDayResponse(
    long Id,
    DateOnly ProductionDate,
    int InitialPlannedQuantity,
    int AddOnQuantity,
    int PlannedQuantity,
    int? ActualQuantity,
    long? ProductionRecordId,
    int ShortageQuantity,
    int? Difference,
    bool HasActiveAdjustment,
    long? ActiveAdjustmentId);

public sealed record ProductionPlanListResponse(long OrderId, IReadOnlyList<ProductionDayResponse> Items);

public sealed record ProductionRecordResponse(
    long Id,
    DateOnly ProductionDate,
    int ActualQuantity,
    AdjustmentRecalculationResponse? AdjustmentRecalculation);

public sealed record AdjustmentRecalculationResponse(
    string Outcome,
    long ReversedAdjustmentId,
    int PreviousShortageQuantity,
    int ShortageQuantity,
    string AdjustmentType,
    long? AdjustmentId,
    IReadOnlyList<PlanAdjustmentItemResponse> Items);

public sealed record AdjustmentPreviewItemResponse(
    long ProductionPlanId, DateOnly ProductionDate, int CurrentPlannedQuantity, int AddOnQuantity, int PlannedQuantityAfter);

public sealed record AdjustmentPreviewResponse(
    long SourceProductionPlanId,
    int ShortageQuantity,
    string AdjustmentType,
    IReadOnlyList<AdjustmentPreviewItemResponse> Items,
    int TotalAddOnQuantity,
    bool Valid,
    string? ValidationCode);

public sealed record PlanAdjustmentItemResponse(long ProductionPlanId, DateOnly ProductionDate, int AddOnQuantity);

public sealed record PlanAdjustmentResponse(
    long Id,
    long SourceProductionPlanId,
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
