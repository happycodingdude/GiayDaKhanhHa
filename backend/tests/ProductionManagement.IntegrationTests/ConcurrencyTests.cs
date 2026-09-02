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
    public async Task Concurrent_entries_can_never_push_the_total_past_the_order_quantity()
    {
        var client = await ClientAsync();
        // Hai ngày mỗi ngày 60 trên đơn hàng 100 đơn vị: nhập riêng lẻ thì vừa, cộng lại thì không.
        var (order, days) = await CreateOrderFromAsync(client, Today.AddDays(-1), 50, 50);

        var first = PostEntryAsync(client, order.Id, days[0].ProductionDate, 60);
        var second = PostEntryAsync(client, order.Id, days[1].ProductionDate, 60);

        var responses = await Task.WhenAll(first, second);

        // Trần ngày là 50 nên cả hai request đều vượt trần ngày trước khi chạm trần đơn; điều đang
        // kiểm ở đây là hai request đồng thời không cùng lọt qua.
        Assert.True(responses.Count(r => r.IsSuccessStatusCode) <= 1);

        var updated = await GetOrderAsync(client, order.Id);
        Assert.True(updated.TotalActual <= updated.Quantity);
    }

    [Fact]
    public async Task Two_concurrent_entries_that_together_exceed_the_daily_plan_leave_exactly_one_in()
    {
        var client = await ClientAsync();
        // AC-22. Hai lần ghi nhận đồng thời, mỗi lần vừa khít trần ngày nhưng cộng lại thì vượt.
        var (order, days) = await CreateOrderAsync(client, 100, 200);

        var responses = await Task.WhenAll(
            PostEntryAsync(client, order.Id, days[0].ProductionDate, 60),
            PostEntryAsync(client, order.Id, days[0].ProductionDate, 60));

        Assert.Equal(1, responses.Count(r => r.IsSuccessStatusCode));
        var rejected = Assert.Single(responses, r => !r.IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);
        Assert.Equal("ENTRY_EXCEEDS_DAILY_PLAN", (await rejected.ReadErrorAsync()).Code);

        var day = await (await GetDayAsync(client, order.Id, days[0].ProductionDate))
            .ReadAsync<ProductionDayDetailResponse>();
        Assert.Single(day.Entries);
        Assert.Equal(60, day.DayActualQuantity);
    }

    [Fact]
    public async Task Two_concurrent_closes_of_the_same_day_leave_exactly_one_winner()
    {
        var client = await ClientAsync();
        // Đóng ngày là thao tác không hoàn tác được, nên nó phải được chặn ở backend (CR-01 §14.7).
        var (order, days) = await CreateOrderAsync(client, 100);
        (await PostEntryAsync(client, order.Id, days[0].ProductionDate, 40)).EnsureSuccessStatusCode();

        var responses = await Task.WhenAll(
            CloseDayAsync(client, order.Id, days[0].ProductionDate),
            CloseDayAsync(client, order.Id, days[0].ProductionDate));

        Assert.Equal(1, responses.Count(r => r.IsSuccessStatusCode));
        var rejected = Assert.Single(responses, r => !r.IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Equal("DAY_ALREADY_CLOSED", (await rejected.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task An_entry_racing_a_close_never_lands_on_a_closed_day()
    {
        var client = await ClientAsync();
        var (order, days) = await CreateOrderAsync(client, 200);
        (await PostEntryAsync(client, order.Id, days[0].ProductionDate, 50)).EnsureSuccessStatusCode();

        var responses = await Task.WhenAll(
            PostEntryAsync(client, order.Id, days[0].ProductionDate, 30),
            CloseDayAsync(client, order.Id, days[0].ProductionDate));

        // Ảnh chụp lúc đóng phải khớp đúng tổng các lần ghi nhận đã lọt vào trước đó.
        var day = await (await GetDayAsync(client, order.Id, days[0].ProductionDate))
            .ReadAsync<ProductionDayDetailResponse>();

        Assert.Equal("Closed", day.DayStatus);
        Assert.Equal(day.Entries.Sum(e => e.Quantity), day.DayActualQuantity);
        Assert.Equal(responses[0].IsSuccessStatusCode ? 80 : 50, day.DayActualQuantity);
    }

    [Fact]
    public async Task Concurrent_applies_for_the_same_source_day_leave_only_one_applied_adjustment()
    {
        var client = await ClientAsync();
        var (order, days) = await CreateOrderAsync(client, 100, 120, 200);
        await RecordAndCloseAsync(client, order.Id, days[0].ProductionDate, 80);

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
        await RecordAndCloseAsync(client, order.Id, days[0].ProductionDate, 80);

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
