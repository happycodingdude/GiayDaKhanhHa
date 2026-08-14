using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace ProductionManagement.IntegrationTests;

/// <summary>
/// The cross-row invariants are protected by transaction + row locking rather than by database
/// triggers or a version column (Step 4 §18). These tests drive the real concurrent paths.
/// </summary>
public class ConcurrencyTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Concurrent_actual_entries_can_never_push_the_total_past_the_order_quantity()
    {
        var client = await ClientAsync();
        // Two days of 60 each on a 100-unit order: either entry alone fits, together they do not.
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
        // Whichever request won, the value is that request's value — never the sum.
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

        // Exactly one target day received the add-on.
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
