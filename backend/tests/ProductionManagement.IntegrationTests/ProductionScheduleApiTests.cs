using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace ProductionManagement.IntegrationTests;

/// <summary>
/// Lập tiến độ và phân bổ hai tầng (CR-001 Slice 3). Các trường hợp dưới đây bám sát checklist §9.
/// </summary>
public class ProductionScheduleApiTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Scheduling_moves_the_order_to_incomplete_and_builds_the_matrix()
    {
        var client = await ClientAsync();
        var order = await ReceiveOrderAsync(client, 1000);

        var l1 = await CreateLineAsync(client, 1);
        var l2 = await CreateLineAsync(client, 2);
        var l3 = await CreateLineAsync(client, 3);

        var response = await PostScheduleAsync(client, order.Id, Today, Today.AddDays(2),
        [
            (l1.Id, 500, [200, 200, 100]),
            (l2.Id, 300, [100, 100, 100]),
            (l3.Id, 200, [70, 70, 60]),
        ]);

        response.EnsureSuccessStatusCode();
        var scheduled = await response.ReadAsync<OrderResponse>();

        Assert.Equal("Incomplete", scheduled.Status);
        Assert.Equal(Today, scheduled.StartDate);
        Assert.Equal(Today.AddDays(2), scheduled.DueDate);
        Assert.Equal(3, scheduled.ProductionLines.Count);
        Assert.Equal(1000, scheduled.ProductionLines.Sum(l => l.AllocatedQuantity));

        var matrix = await GetMatrixAsync(client, order.Id);
        Assert.Equal(9, matrix.Items.Count);
        Assert.Equal(1000, matrix.Items.Sum(i => i.InitialPlannedQuantity));

        // Mỗi dây chuyền: tổng kế hoạch ban đầu bằng đúng số đã phân cho nó (BR-N08b).
        foreach (var line in matrix.ProductionLines)
        {
            Assert.Equal(
                line.AllocatedQuantity,
                matrix.Items.Where(i => i.ProductionLineId == line.Id).Sum(i => i.InitialPlannedQuantity));
        }
    }

    [Fact]
    public async Task Tier_one_mismatch_is_rejected_and_nothing_is_stored()
    {
        var client = await ClientAsync();
        var order = await ReceiveOrderAsync(client, 1000);
        var line = await CreateLineAsync(client);

        var response = await PostScheduleAsync(client, order.Id, Today, Today, [(line.Id, 900, [900])]);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("LINE_ALLOCATION_MISMATCH", (await response.ReadErrorAsync()).Code);

        // Transaction rollback: đơn vẫn Pending và chưa có ô nào.
        Assert.Equal("Pending", (await GetOrderAsync(client, order.Id)).Status);
        Assert.Empty(await GetCellsAsync(client, order.Id));
    }

    [Fact]
    public async Task Tier_two_mismatch_names_exactly_the_line_that_is_off()
    {
        var client = await ClientAsync();
        var order = await ReceiveOrderAsync(client, 1000);
        var l1 = await CreateLineAsync(client, 1);
        var l2 = await CreateLineAsync(client, 2);

        var response = await PostScheduleAsync(client, order.Id, Today, Today.AddDays(1),
        [
            (l1.Id, 600, [300, 300]),
            // Dây chuyền 2 được phân 400 nhưng kế hoạch ngày chỉ cộng ra 350.
            (l2.Id, 400, [200, 150]),
        ]);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var error = await response.ReadErrorAsync();

        Assert.Equal("LINE_PLAN_TOTAL_MISMATCH", error.Code);
        var detail = Assert.Single(error.Details!);
        Assert.Equal(l2.Id.ToString(), detail.Field);
        Assert.Equal("350/400", detail.Message);
    }

    [Fact]
    public async Task A_line_allocated_zero_is_rejected()
    {
        var client = await ClientAsync();
        var order = await ReceiveOrderAsync(client, 1000);
        var l1 = await CreateLineAsync(client, 1);
        var l2 = await CreateLineAsync(client, 2);

        var response = await PostScheduleAsync(client, order.Id, Today, Today,
        [
            (l1.Id, 1000, [1000]),
            (l2.Id, 0, [0]),
        ]);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("PRODUCTION_LINE_EMPTY_ALLOCATION", (await response.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task Scheduling_the_same_order_twice_is_a_conflict()
    {
        var client = await ClientAsync();
        var (order, lineId, _) = await CreateOrderAsync(client, 100);

        var response = await PostScheduleAsync(client, order.Id, Today, Today, [(lineId, 100, [100])]);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("ORDER_SCHEDULE_ALREADY_EXISTS", (await response.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task An_inactive_line_cannot_be_scheduled()
    {
        var client = await ClientAsync();
        var order = await ReceiveOrderAsync(client, 100);
        var line = await CreateLineAsync(client);

        (await client.PatchAsJsonAsync(
            $"/api/v1/production-lines/{line.Id}/status", new { status = "Inactive" })).EnsureSuccessStatusCode();

        var response = await PostScheduleAsync(client, order.Id, Today, Today, [(line.Id, 100, [100])]);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("PRODUCTION_LINE_INACTIVE", (await response.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task An_unknown_line_returns_a_not_found_error()
    {
        var client = await ClientAsync();
        var order = await ReceiveOrderAsync(client, 100);

        var response = await PostScheduleAsync(client, order.Id, Today, Today, [(Guid.NewGuid(), 100, [100])]);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("PRODUCTION_LINE_NOT_FOUND", (await response.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task A_cell_planned_for_zero_creates_no_plan_row()
    {
        var client = await ClientAsync();
        var order = await ReceiveOrderAsync(client, 300);
        var line = await CreateLineAsync(client);

        // Dây chuyền nghỉ ngày giữa (CR-001 §6.6b, BR-N10).
        (await PostScheduleAsync(client, order.Id, Today, Today.AddDays(2),
            [(line.Id, 300, [150, 0, 150])])).EnsureSuccessStatusCode();

        var cells = await GetCellsAsync(client, order.Id);

        Assert.Equal(2, cells.Count);
        Assert.DoesNotContain(cells, c => c.ProductionDate == Today.AddDays(1));
    }

    [Fact]
    public async Task Two_lines_can_produce_on_the_same_date()
    {
        var client = await ClientAsync();
        var order = await ReceiveOrderAsync(client, 200);
        var l1 = await CreateLineAsync(client, 1);
        var l2 = await CreateLineAsync(client, 2);

        // Chính là bất biến mới: khoá của ô là (đơn, ngày, dây chuyền) chứ không phải (đơn, ngày).
        (await PostScheduleAsync(client, order.Id, Today, Today,
            [(l1.Id, 120, [120]), (l2.Id, 80, [80])])).EnsureSuccessStatusCode();

        var cells = await GetCellsAsync(client, order.Id);

        Assert.Equal(2, cells.Count);
        Assert.All(cells, c => Assert.Equal(Today, c.ProductionDate));
    }

    [Fact]
    public async Task A_due_date_before_the_start_date_is_a_validation_error()
    {
        var client = await ClientAsync();
        var order = await ReceiveOrderAsync(client, 100);
        var line = await CreateLineAsync(client);

        var response = await PostScheduleAsync(
            client, order.Id, Today.AddDays(3), Today, [(line.Id, 100, [100])]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", (await response.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task Recording_production_on_an_order_without_a_schedule_is_rejected()
    {
        var client = await ClientAsync();
        var order = await ReceiveOrderAsync(client, 100);
        var line = await CreateLineAsync(client);

        var response = await PostEntryAsync(client, order.Id, Today, line.Id, 10);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("ORDER_NOT_SCHEDULED", (await response.ReadErrorAsync()).Code);
    }
}
