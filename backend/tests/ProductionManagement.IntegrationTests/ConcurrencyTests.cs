using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace ProductionManagement.IntegrationTests;

/// <summary>
/// Các bất biến liên dòng được bảo vệ bằng transaction + row lock chứ không phải bằng trigger
/// database hay cột version (Step 4 §18). Các test này chạy thật những đường đi đồng thời.
/// </summary>
public class ConcurrencyTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Concurrent_actual_entries_can_never_push_the_total_past_the_order_quantity()
    {
        var client = await ClientAsync();
        // Hai ngày mỗi ngày 60 trên đơn hàng 100 đơn vị: nhập riêng lẻ thì vừa, cộng lại thì không.
        var (order, days) = await CreateOrderFromAsync(client, Today.AddDays(-1), 50, 50);

        var first = PostActualAsync(client, order.Id, days[0].ProductionDate, 60);
        var second = PostActualAsync(client, order.Id, days[1].ProductionDate, 60);

        var responses = await Task.WhenAll(first, second);

        Assert.Equal(1, responses.Count(r => r.IsSuccessStatusCode));
        var rejected = Assert.Single(responses, r => !r.IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);
        Assert.Equal("ACTUAL_EXCEEDS_ORDER_QUANTITY", (await rejected.ReadErrorAsync()).Code);

        var updated = await GetOrderAsync(client, order.Id);
        Assert.Equal(60, updated.TotalActual);
        Assert.True(updated.TotalActual <= updated.Quantity);
    }

    [Fact]
    public async Task Concurrent_entries_for_the_same_day_produce_exactly_one_record()
    {
        var client = await ClientAsync();
        var (order, days) = await CreateOrderAsync(client, 100);

        var responses = await Task.WhenAll(
            PostActualAsync(client, order.Id, days[0].ProductionDate, 30),
            PostActualAsync(client, order.Id, days[0].ProductionDate, 40));

        Assert.Equal(1, responses.Count(r => r.IsSuccessStatusCode));
        Assert.Equal(HttpStatusCode.Conflict, Assert.Single(responses, r => !r.IsSuccessStatusCode).StatusCode);

        var updated = await GetDaysAsync(client, order.Id);
        Assert.NotNull(updated[0].ProductionRecordId);
        // Request nào thắng thì giá trị là của request đó — không bao giờ là tổng hai bên.
        Assert.Contains(updated[0].ActualQuantity, new int?[] { 30, 40 });
    }

    [Fact]
    public async Task Concurrent_applies_for_the_same_source_day_leave_only_one_applied_adjustment()
    {
        var client = await ClientAsync();
        var (order, days) = await CreateOrderAsync(client, 100, 120, 200);
        (await PostActualAsync(client, order.Id, days[0].ProductionDate, 80)).EnsureSuccessStatusCode();

        async Task<HttpResponseMessage> Apply(Guid targetPlanId) =>
            await client.PostAsJsonAsync($"/api/v1/production-plans/{days[0].Id}/adjustments", new
            {
                adjustmentType = "Manual",
                shortageQuantity = 20,
                targets = new[] { new { productionPlanId = targetPlanId, addOnQuantity = 20 } },
            });

        var responses = await Task.WhenAll(Apply(days[1].Id), Apply(days[2].Id));

        Assert.Equal(1, responses.Count(r => r.IsSuccessStatusCode));
        Assert.Equal(HttpStatusCode.Conflict, Assert.Single(responses, r => !r.IsSuccessStatusCode).StatusCode);

        var history = await (await client.GetAsync($"/api/v1/orders/{order.Id}/plan-adjustments"))
            .ReadAsync<List<PlanAdjustmentResponse>>();
        Assert.Single(history);

        // Đúng một ngày đích nhận được khoản bù.
        var after = await GetDaysAsync(client, order.Id);
        Assert.Equal(20, after.Sum(day => day.AddOnQuantity));
    }

    [Fact]
    public async Task Concurrent_reverses_of_the_same_adjustment_only_subtract_the_add_on_once()
    {
        var client = await ClientAsync();
        var (order, days) = await CreateOrderAsync(client, 100, 120);
        (await PostActualAsync(client, order.Id, days[0].ProductionDate, 80)).EnsureSuccessStatusCode();

        var applied = await (await client.PostAsJsonAsync(
            $"/api/v1/production-plans/{days[0].Id}/adjustments", new
            {
                adjustmentType = "Manual",
                shortageQuantity = 20,
                targets = new[] { new { productionPlanId = days[1].Id, addOnQuantity = 20 } },
            })).ReadAsync<PlanAdjustmentResponse>();

        var responses = await Task.WhenAll(
            client.PostAsync($"/api/v1/plan-adjustments/{applied.Id}/reverse", null),
            client.PostAsync($"/api/v1/plan-adjustments/{applied.Id}/reverse", null));

        Assert.Equal(1, responses.Count(r => r.IsSuccessStatusCode));

        var after = await GetDaysAsync(client, order.Id);
        Assert.Equal(120, after[1].PlannedQuantity);
        Assert.Equal(0, after[1].AddOnQuantity);
    }
}
