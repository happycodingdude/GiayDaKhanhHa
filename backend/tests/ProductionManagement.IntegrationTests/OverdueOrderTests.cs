using System.Net;
using System.Net.Http.Json;
using Npgsql;
using Xunit;

namespace ProductionManagement.IntegrationTests;

/// <summary>
/// Đơn hàng đã qua ngày hạn là chỉ đọc, bất kể trạng thái nào. Mọi endpoint ghi vào một đơn hàng
/// đã tồn tại đều phải từ chối, và mọi thao tác đọc vẫn phải chạy được.
/// </summary>
public class OverdueOrderTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    /// <summary>
    /// Đẩy toàn bộ kỳ sản xuất của đơn hàng về quá khứ. Test không tua được thời gian và không
    /// endpoint nào đổi được ngày của đơn hàng, nên đơn được làm "già" thẳng trong database dùng
    /// một lần. Cả hai mốc ngày đều dịch để ràng buộc ck_orders_date_range vẫn đúng.
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

    /// <summary>Đơn hàng thiếu 20 đơn vị ở ngày đầu đã Xuất hàng, sau đó bị đẩy qua ngày hạn.</summary>
    private async Task<(HttpClient Client, OrderResponse Order, IReadOnlyList<ProductionDayResponse> Days)>
        OverdueOrderWithShortageAsync()
    {
        var client = await ClientAsync();
        var (order, days) = await CreateOrderAsync(client, 100, 120);

        await RecordAndCloseAsync(client, order.Id, days[0].ProductionDate, 80);

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
    public async Task Recording_an_entry_is_rejected_on_an_overdue_order()
    {
        var (client, order, days) = await OverdueOrderWithShortageAsync();

        await AssertOverdueRejectionAsync(
            await PostEntryAsync(client, order.Id, days[1].ProductionDate, 50));

        // Không có gì được ghi: ngày thứ hai vẫn hoàn toàn chưa có lần ghi nhận nào.
        Assert.Null((await GetDaysAsync(client, order.Id))[1].ActualQuantity);
    }

    [Fact]
    public async Task Closing_a_day_is_rejected_on_an_overdue_order()
    {
        var (client, order, days) = await OverdueOrderWithShortageAsync();

        await AssertOverdueRejectionAsync(await CloseDayAsync(client, order.Id, days[1].ProductionDate));
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

        // Không khoản bù nào tới được ngày đích.
        Assert.Equal(days[1].PlannedQuantity, (await GetDaysAsync(client, order.Id))[1].PlannedQuantity);
    }

    [Fact]
    public async Task Reversing_an_adjustment_is_rejected_once_the_order_is_overdue()
    {
        // Điều chỉnh được áp dụng khi đơn hàng còn trong hạn, sau đó ngày hạn mới trôi qua.
        var client = await ClientAsync();
        var (order, days) = await CreateOrderAsync(client, 100, 120);

        await RecordAndCloseAsync(client, order.Id, days[0].ProductionDate, 80);

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

        // Khoản bù vẫn nằm trên kế hoạch và bản ghi vẫn ở trạng thái Applied.
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

        await RecordAndCloseAsync(client, order.Id, days[0].ProductionDate, 100);
        (await PostEntryAsync(client, order.Id, days[1].ProductionDate, 100)).EnsureSuccessStatusCode();
        await RecordAndCloseAsync(client, order.Id, days[1].ProductionDate, 0);

        await MakeOverdueAsync(order.Id);

        var completed = await GetOrderAsync(client, order.Id);
        Assert.Equal("Completed", completed.Status);
        // Đã giao đủ nên không bị báo là trễ — nhưng kỳ sản xuất thì đã kết thúc.
        Assert.False(completed.IsOverdue);
        Assert.True(completed.IsPastDueDate);

        await AssertOverdueRejectionAsync(
            await PostEntryAsync(client, order.Id, days[1].ProductionDate, 5));

        Assert.Equal(100, (await GetDaysAsync(client, order.Id))[1].ActualQuantity);
    }

    [Fact]
    public async Task An_order_is_still_editable_on_its_due_date()
    {
        var client = await ClientAsync();
        // Kỳ sản xuất kết thúc hôm nay, nên hôm nay là ngày hạn và vẫn nằm trong kỳ.
        var (order, days) = await CreateOrderAsync(client, 100);

        var detail = await GetOrderAsync(client, order.Id);
        Assert.False(detail.IsPastDueDate);

        (await PostEntryAsync(client, order.Id, days[0].ProductionDate, 60)).EnsureSuccessStatusCode();
    }
}
