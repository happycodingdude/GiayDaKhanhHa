using System.Net.Http.Json;
using Xunit;

namespace ProductionManagement.IntegrationTests;

[Collection(nameof(ApiCollection))]
public abstract class IntegrationTestBase(ApiFactory factory)
{
    protected ApiFactory Factory { get; } = factory;

    /// <summary>The API's business date. Adjustment eligibility depends on it.</summary>
    protected DateOnly Today => Factory.Today;

    protected Task<HttpClient> ClientAsync() => Factory.CreateAuthenticatedClientAsync();

    private static int _sequence;

    protected static string NextOrderCode() => $"ORD-{Interlocked.Increment(ref _sequence):D5}-{Guid.NewGuid():N}"[..20];

    /// <summary>
    /// Creates an order whose production period starts today, so every later day is a valid
    /// adjustment target.
    /// </summary>
    protected async Task<(OrderResponse Order, IReadOnlyList<ProductionDayResponse> Days)> CreateOrderAsync(
        HttpClient client, params int[] dailyPlan)
    {
        var plans = dailyPlan
            .Select((quantity, index) => new
            {
                productionDate = Today.AddDays(index).ToString("yyyy-MM-dd"),
                plannedQuantity = quantity,
            })
            .ToArray();

        var response = await client.PostAsJsonAsync("/api/v1/orders", new
        {
            orderCode = NextOrderCode(),
            quantity = dailyPlan.Sum(),
            startDate = Today.ToString("yyyy-MM-dd"),
            dueDate = Today.AddDays(dailyPlan.Length - 1).ToString("yyyy-MM-dd"),
            productionPlans = plans,
        });

        response.EnsureSuccessStatusCode();
        var order = await response.ReadAsync<OrderResponse>();

        return (order, await GetDaysAsync(client, order.Id));
    }

    protected static async Task<IReadOnlyList<ProductionDayResponse>> GetDaysAsync(HttpClient client, long orderId)
    {
        var response = await client.GetAsync($"/api/v1/orders/{orderId}/production-plans");
        response.EnsureSuccessStatusCode();
        return (await response.ReadAsync<ProductionPlanListResponse>()).Items;
    }

    protected static async Task<OrderResponse> GetOrderAsync(HttpClient client, long orderId)
    {
        var response = await client.GetAsync($"/api/v1/orders/{orderId}");
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<OrderResponse>();
    }

    protected static Task<HttpResponseMessage> PostActualAsync(
        HttpClient client, long orderId, DateOnly date, int actualQuantity)
        => client.PostAsJsonAsync($"/api/v1/orders/{orderId}/production-records", new
        {
            productionDate = date.ToString("yyyy-MM-dd"),
            actualQuantity,
        });

    protected static Task<HttpResponseMessage> PutActualAsync(
        HttpClient client, long orderId, long recordId, int actualQuantity)
        => client.PutAsJsonAsync(
            $"/api/v1/orders/{orderId}/production-records/{recordId}", new { actualQuantity });
}
