using System.Net;
using System.Net.Http.Json;
using Npgsql;
using Xunit;

namespace ProductionManagement.IntegrationTests;

/// <summary>
/// An order whose due date has passed is read-only, whatever its status. Every endpoint that
/// writes to an existing order must refuse it, and every read must keep working.
/// </summary>
public class OverdueOrderTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    /// <summary>
    /// Moves an order's whole production period into the past. Time cannot be advanced from a test
    /// and no endpoint can change an order's dates, so the order is aged directly in the throwaway
    /// database. Both dates move so the ck_orders_date_range check still holds.
    /// </summary>
    private async Task MakeOverdueAsync(Guid orderId)
    {
        await using var connection = new NpgsqlConnection(Factory.TestConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "UPDATE orders SET start_date = @start, due_date = @due WHERE id = @id", connection);
        command.Parameters.AddWithValue("start", Today.AddDays(-10));
        command.Parameters.AddWithValue("due", Today.AddDays(-1));
        command.Parameters.AddWithValue("id", orderId);

        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    /// <summary>An order with a 20-unit shortage on its first day, aged past its due date.</summary>
    private async Task<(HttpClient Client, OrderResponse Order, IReadOnlyList<ProductionDayResponse> Days)>
        OverdueOrderWithShortageAsync()
    {
        var client = await ClientAsync();
        var (order, days) = await CreateOrderAsync(client, 100, 120);

        (await PostActualAsync(client, order.Id, days[0].ProductionDate, 80)).EnsureSuccessStatusCode();

        var current = await GetDaysAsync(client, order.Id);
        await MakeOverdueAsync(order.Id);

        return (client, order, current);
    }

    private static async Task AssertOverdueRejectionAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("ORDER_OVERDUE", (await response.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task An_order_past_its_due_date_is_reported_as_overdue_and_locked()
    {
        var (client, order, _) = await OverdueOrderWithShortageAsync();

        var detail = await GetOrderAsync(client, order.Id);
        Assert.True(detail.IsOverdue);
        Assert.True(detail.IsPastDueDate);
    }

    [Fact]
    public async Task Recording_an_actual_is_rejected_on_an_overdue_order()
    {
        var (client, order, days) = await OverdueOrderWithShortageAsync();

        await AssertOverdueRejectionAsync(
            await PostActualAsync(client, order.Id, days[1].ProductionDate, 50));

        // Nothing was written: the second day still has no record at all.
        Assert.Null((await GetDaysAsync(client, order.Id))[1].ActualQuantity);
    }

    [Fact]
    public async Task Editing_an_existing_actual_is_rejected_on_an_overdue_order()
    {
        var (client, order, days) = await OverdueOrderWithShortageAsync();
        var recordId = days[0].ProductionRecordId!.Value;

        await AssertOverdueRejectionAsync(await PutActualAsync(client, order.Id, recordId, 95));

        Assert.Equal(80, (await GetDaysAsync(client, order.Id))[0].ActualQuantity);
    }

    [Fact]
    public async Task Previewing_an_adjustment_is_rejected_on_an_overdue_order()
    {
        var (client, _, days) = await OverdueOrderWithShortageAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/production-plans/{days[0].Id}/adjustments/preview",
            new { adjustmentType = "Automatic" });

        await AssertOverdueRejectionAsync(response);
    }

    [Fact]
    public async Task Applying_an_adjustment_is_rejected_on_an_overdue_order()
    {
        var (client, order, days) = await OverdueOrderWithShortageAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/production-plans/{days[0].Id}/adjustments",
            new
            {
                adjustmentType = "Manual",
                shortageQuantity = 20,
                targets = new[] { new { productionPlanId = days[1].Id, addOnQuantity = 20 } },
            });

        await AssertOverdueRejectionAsync(response);

        // No add-on reached the target day.
        Assert.Equal(days[1].PlannedQuantity, (await GetDaysAsync(client, order.Id))[1].PlannedQuantity);
    }

    [Fact]
    public async Task Reversing_an_adjustment_is_rejected_once_the_order_is_overdue()
    {
        // The adjustment is applied while the order is still on time, then the due date passes.
        var client = await ClientAsync();
        var (order, days) = await CreateOrderAsync(client, 100, 120);

        (await PostActualAsync(client, order.Id, days[0].ProductionDate, 80)).EnsureSuccessStatusCode();

        var applyResponse = await client.PostAsJsonAsync(
            $"/api/v1/production-plans/{days[0].Id}/adjustments",
            new
            {
                adjustmentType = "Manual",
                shortageQuantity = 20,
                targets = new[] { new { productionPlanId = days[1].Id, addOnQuantity = 20 } },
            });

        applyResponse.EnsureSuccessStatusCode();
        var adjustment = await applyResponse.ReadAsync<PlanAdjustmentResponse>();

        await MakeOverdueAsync(order.Id);

        await AssertOverdueRejectionAsync(
            await client.PostAsync($"/api/v1/plan-adjustments/{adjustment.Id}/reverse", null));

        // The add-on stays on the plan and the entry stays Applied.
        Assert.Equal(days[1].PlannedQuantity + 20, (await GetDaysAsync(client, order.Id))[1].PlannedQuantity);

        var history = await (await client.GetAsync($"/api/v1/orders/{order.Id}/plan-adjustments"))
            .ReadAsync<List<PlanAdjustmentResponse>>();
        Assert.Equal("Applied", history[0].Status);
    }

    [Fact]
    public async Task An_overdue_order_can_still_be_read_in_full()
    {
        var (client, order, _) = await OverdueOrderWithShortageAsync();

        foreach (var path in new[]
                 {
                     $"/api/v1/orders/{order.Id}",
                     $"/api/v1/orders/{order.Id}/production-plans",
                     $"/api/v1/orders/{order.Id}/plan-adjustments",
                     $"/api/v1/orders/{order.Id}/statistics",
                 })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task A_completed_order_past_its_due_date_is_locked_even_though_it_is_not_late()
    {
        var client = await ClientAsync();
        var (order, days) = await CreateOrderFromAsync(client, Today.AddDays(-1), 100, 100);

        (await PostActualAsync(client, order.Id, days[0].ProductionDate, 100)).EnsureSuccessStatusCode();
        (await PostActualAsync(client, order.Id, days[1].ProductionDate, 100)).EnsureSuccessStatusCode();

        var recordId = (await GetDaysAsync(client, order.Id))[1].ProductionRecordId!.Value;
        await MakeOverdueAsync(order.Id);

        var completed = await GetOrderAsync(client, order.Id);
        Assert.Equal("Completed", completed.Status);
        // Delivered in full, so it is not reported as late — but the period is over.
        Assert.False(completed.IsOverdue);
        Assert.True(completed.IsPastDueDate);

        await AssertOverdueRejectionAsync(await PutActualAsync(client, order.Id, recordId, 90));

        Assert.Equal(100, (await GetDaysAsync(client, order.Id))[1].ActualQuantity);
    }

    [Fact]
    public async Task An_order_is_still_editable_on_its_due_date()
    {
        var client = await ClientAsync();
        // The production period ends today, so today is the due date and still inside the period.
        var (order, days) = await CreateOrderAsync(client, 100);

        var detail = await GetOrderAsync(client, order.Id);
        Assert.False(detail.IsPastDueDate);

        (await PostActualAsync(client, order.Id, days[0].ProductionDate, 60)).EnsureSuccessStatusCode();
    }
}
