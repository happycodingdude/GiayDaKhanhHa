using System.Net.Http.Json;
using Xunit;

namespace ProductionManagement.IntegrationTests;

[Collection(nameof(ApiCollection))]
public abstract class IntegrationTestBase(ApiFactory factory)
{
    protected ApiFactory Factory { get; } = factory;

    /// <summary>Ngày nghiệp vụ của API. Điều kiện hợp lệ của điều chỉnh phụ thuộc vào nó.</summary>
    protected DateOnly Today => Factory.Today;

    protected Task<HttpClient> ClientAsync() => Factory.CreateAuthenticatedClientAsync();

    private static int _sequence;

    protected static string NextOrderCode() => $"ORD-{Interlocked.Increment(ref _sequence):D5}-{Guid.NewGuid():N}"[..20];

    /// <summary>
    /// Tạo đơn hàng có kỳ sản xuất bắt đầu từ hôm nay, nên mọi ngày sau đó đều là ngày đích hợp lệ
    /// để điều chỉnh. Chỉ ngày đầu tiên nhập được thực tế — các ngày còn lại nằm ở tương lai.
    /// </summary>
    protected Task<(OrderResponse Order, IReadOnlyList<ProductionDayResponse> Days)> CreateOrderAsync(
        HttpClient client, params int[] dailyPlan)
        => CreateOrderFromAsync(client, Today, dailyPlan);

    /// <summary>
    /// Tạo đơn hàng có kỳ sản xuất bắt đầu vào <paramref name="startDate"/>. Dùng ngày bắt đầu
    /// trong quá khứ khi test cần ghi thực tế cho nhiều hơn một ngày: thực tế chỉ ghi được tới hôm
    /// nay.
    /// </summary>
    protected async Task<(OrderResponse Order, IReadOnlyList<ProductionDayResponse> Days)> CreateOrderFromAsync(
        HttpClient client, DateOnly startDate, params int[] dailyPlan)
    {
        var plans = dailyPlan
            .Select((quantity, index) => new
            {
                productionDate = startDate.AddDays(index).ToString("yyyy-MM-dd"),
                plannedQuantity = quantity,
            })
            .ToArray();

        var response = await client.PostAsJsonAsync("/api/v1/orders", new
        {
            orderCode = NextOrderCode(),
            quantity = dailyPlan.Sum(),
            startDate = startDate.ToString("yyyy-MM-dd"),
            dueDate = startDate.AddDays(dailyPlan.Length - 1).ToString("yyyy-MM-dd"),
            productionPlans = plans,
        });

        response.EnsureSuccessStatusCode();
        var order = await response.ReadAsync<OrderResponse>();

        return (order, await GetDaysAsync(client, order.Id));
    }

    protected static async Task<IReadOnlyList<ProductionDayResponse>> GetDaysAsync(HttpClient client, Guid orderId)
    {
        var response = await client.GetAsync($"/api/v1/orders/{orderId}/production-plans");
        response.EnsureSuccessStatusCode();
        return (await response.ReadAsync<ProductionPlanListResponse>()).Items;
    }

    protected static async Task<OrderResponse> GetOrderAsync(HttpClient client, Guid orderId)
    {
        var response = await client.GetAsync($"/api/v1/orders/{orderId}");
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<OrderResponse>();
    }

    protected static Task<HttpResponseMessage> GetDayAsync(HttpClient client, Guid orderId, DateOnly date)
        => client.GetAsync($"/api/v1/orders/{orderId}/production-days/{date:yyyy-MM-dd}");

    protected static Task<HttpResponseMessage> PostEntryAsync(
        HttpClient client, Guid orderId, DateOnly date, int quantity, string? note = null)
        => client.PostAsJsonAsync(
            $"/api/v1/orders/{orderId}/production-days/{date:yyyy-MM-dd}/entries", new { quantity, note });

    protected static Task<HttpResponseMessage> PutEntryAsync(
        HttpClient client, Guid entryId, int quantity, string? note = null)
        => client.PutAsJsonAsync($"/api/v1/production-entries/{entryId}", new { quantity, note });

    protected static Task<HttpResponseMessage> DeleteEntryAsync(HttpClient client, Guid entryId)
        => client.DeleteAsync($"/api/v1/production-entries/{entryId}");

    protected static Task<HttpResponseMessage> CloseDayAsync(HttpClient client, Guid orderId, DateOnly date)
        => client.PostAsync($"/api/v1/orders/{orderId}/production-days/{date:yyyy-MM-dd}/close", null);

    /// <summary>
    /// Ghi nhận rồi chốt sổ ngay — cách nhanh nhất để dựng một ngày đã Xuất hàng, thứ mà mọi test về
    /// phần thiếu và điều chỉnh đều cần làm trước.
    /// </summary>
    protected static async Task<ProductionDayDetailResponse> RecordAndCloseAsync(
        HttpClient client, Guid orderId, DateOnly date, int quantity)
    {
        if (quantity > 0)
        {
            (await PostEntryAsync(client, orderId, date, quantity)).EnsureSuccessStatusCode();
        }

        var response = await CloseDayAsync(client, orderId, date);
        response.EnsureSuccessStatusCode();

        var day = await GetDayAsync(client, orderId, date);
        return await day.ReadAsync<ProductionDayDetailResponse>();
    }
}
