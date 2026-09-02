using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace ProductionManagement.IntegrationTests;

public class OrderApiTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Creating_an_order_also_creates_its_initial_production_plans()
    {
        var client = await ClientAsync();

        var (order, days) = await CreateOrderAsync(client, 100, 120, 200, 250, 330);

        Assert.Equal(1000, order.Quantity);
        Assert.Equal("Incomplete", order.Status);
        Assert.Equal(5, days.Count);
        Assert.Equal(1000, days.Sum(d => d.InitialPlannedQuantity));
        Assert.Equal(1000, days.Sum(d => d.PlannedQuantity));
        Assert.All(days, day => Assert.Null(day.ActualQuantity));
    }

    [Fact]
    public async Task An_initial_plan_total_that_differs_from_the_quantity_is_rejected_and_nothing_is_stored()
    {
        var client = await ClientAsync();
        var orderCode = NextOrderCode();

        var response = await client.PostAsJsonAsync("/api/v1/orders", new
        {
            orderCode,
            quantity = 100,
            startDate = Today.ToString("yyyy-MM-dd"),
            dueDate = Today.AddDays(1).ToString("yyyy-MM-dd"),
            productionPlans = new[]
            {
                new { productionDate = Today.ToString("yyyy-MM-dd"), plannedQuantity = 40 },
                new { productionDate = Today.AddDays(1).ToString("yyyy-MM-dd"), plannedQuantity = 50 },
            },
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("INITIAL_PLAN_TOTAL_MISMATCH", (await response.ReadErrorAsync()).Code);

        // Đơn hàng và các kế hoạch của nó được tạo trong một transaction, nên không sót lại gì.
        var list = await client.GetAsync($"/api/v1/orders?search={orderCode}");
        var body = await list.Content.ReadAsStringAsync();
        Assert.DoesNotContain(orderCode, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_duplicate_order_code_is_rejected_with_a_conflict()
    {
        var client = await ClientAsync();
        var orderCode = NextOrderCode();

        object Payload() => new
        {
            orderCode,
            quantity = 10,
            startDate = Today.ToString("yyyy-MM-dd"),
            dueDate = Today.ToString("yyyy-MM-dd"),
            productionPlans = new[] { new { productionDate = Today.ToString("yyyy-MM-dd"), plannedQuantity = 10 } },
        };

        var first = await client.PostAsJsonAsync("/api/v1/orders", Payload());
        first.EnsureSuccessStatusCode();

        var second = await client.PostAsJsonAsync("/api/v1/orders", Payload());

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("ORDER_CODE_ALREADY_EXISTS", (await second.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task A_due_date_before_the_start_date_is_a_validation_error()
    {
        var client = await ClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/orders", new
        {
            orderCode = NextOrderCode(),
            quantity = 10,
            startDate = Today.AddDays(3).ToString("yyyy-MM-dd"),
            dueDate = Today.ToString("yyyy-MM-dd"),
            productionPlans = new[] { new { productionDate = Today.AddDays(3).ToString("yyyy-MM-dd"), plannedQuantity = 10 } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", (await response.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task A_day_planned_for_zero_is_accepted_when_the_total_still_matches()
    {
        var client = await ClientAsync();

        var (_, days) = await CreateOrderAsync(client, 50, 0, 50);

        Assert.Equal(3, days.Count);
        Assert.Contains(days, day => day.PlannedQuantity == 0);
    }

    [Fact]
    public async Task Requesting_a_missing_order_returns_a_not_found_error()
    {
        var client = await ClientAsync();

        // Một id đúng định dạng nhưng không đơn hàng nào có. Id sai định dạng sẽ không khớp route
        // {orderId:guid}, và trả về 404 do routing, không kèm body lỗi mà test này đang kiểm.
        var response = await client.GetAsync($"/api/v1/orders/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("ORDER_NOT_FOUND", (await response.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task The_order_list_can_be_filtered_by_status()
    {
        var client = await ClientAsync();
        var (order, days) = await CreateOrderAsync(client, 10);

        // Trạng thái đơn hàng chỉ được đánh giá tại thời điểm Xuất hàng (CR-01 OV-4).
        await RecordAndCloseAsync(client, order.Id, days[0].ProductionDate, 10);

        var completed = await client.GetAsync("/api/v1/orders?status=Completed&pageSize=200");
        completed.EnsureSuccessStatusCode();
        Assert.Contains(order.OrderCode, await completed.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var incomplete = await client.GetAsync("/api/v1/orders?status=Incomplete&pageSize=200");
        incomplete.EnsureSuccessStatusCode();
        Assert.DoesNotContain(order.OrderCode, await incomplete.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }
}
