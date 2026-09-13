using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace ProductionManagement.IntegrationTests;

/// <summary>
/// Ma trận ngày × dây chuyền và ràng buộc bù trong cùng dây chuyền (CR-001 Slice 4 &amp; 5).
/// Đây là những bất biến mà một đơn hàng một dây chuyền không thể kiểm được.
/// </summary>
public class MultiLineProductionApiTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    /// <summary>
    /// Đơn 400 đôi trên hai dây chuyền, hai ngày, bắt đầu từ hôm qua để cả hai ngày đều ghi được
    /// thực tế. Dây chuyền 1: 100/100, dây chuyền 2: 100/100.
    /// </summary>
    private async Task<(HttpClient Client, OrderResponse Order, ProductionLineResponse L1, ProductionLineResponse L2,
        IReadOnlyList<ProductionCellResponse> Cells)> TwoLineOrderAsync()
    {
        var client = await ClientAsync();
        var order = await ReceiveOrderAsync(client, 400);
        var l1 = await CreateLineAsync(client, 1);
        var l2 = await CreateLineAsync(client, 2);

        var start = Today.AddDays(-1);
        (await PostScheduleAsync(client, order.Id, start, Today,
            [(l1.Id, 200, [100, 100]), (l2.Id, 200, [100, 100])])).EnsureSuccessStatusCode();

        return (client, order, l1, l2, await GetCellsAsync(client, order.Id));
    }

    private static ProductionCellResponse Cell(
        IReadOnlyList<ProductionCellResponse> cells, DateOnly date, Guid lineId)
        => cells.Single(c => c.ProductionDate == date && c.ProductionLineId == lineId);

    [Fact]
    public async Task Closing_a_day_closes_every_line_planned_on_that_day_at_once()
    {
        var (client, order, l1, l2, _) = await TwoLineOrderAsync();
        var yesterday = Today.AddDays(-1);

        // Dây chuyền 2 không ghi nhận lần nào: vẫn bị đóng cùng ngày, với sản lượng 0.
        (await PostEntryAsync(client, order.Id, yesterday, l1.Id, 90)).EnsureSuccessStatusCode();

        var response = await CloseDayAsync(client, order.Id, yesterday);
        response.EnsureSuccessStatusCode();

        var closed = await response.ReadAsync<CloseProductionDayResponse>();
        Assert.True(closed.HasShortage);
        // Cùng thứ tự với cột của ma trận.
        Assert.Equal(new[] { l1.Id, l2.Id }, closed.Cells.Select(c => c.ProductionLineId));
        Assert.Equal(90, closed.Cells[0].ActualQuantity);
        Assert.Equal(10, closed.Cells[0].ShortageQuantity);
        Assert.Equal(0, closed.Cells[1].ActualQuantity);
        Assert.Equal(100, closed.Cells[1].ShortageQuantity);

        var after = await GetCellsAsync(client, order.Id);

        var line1 = Cell(after, yesterday, l1.Id);
        Assert.Equal("Closed", line1.DayStatus);
        Assert.Equal(-10, line1.Difference);

        var line2 = Cell(after, yesterday, l2.Id);
        Assert.Equal("Closed", line2.DayStatus);
        Assert.Equal(100, line2.ShortageQuantity);

        // Ngày khác không bị đụng tới.
        Assert.All(after.Where(c => c.ProductionDate == Today), c => Assert.Equal("InProduction", c.DayStatus));
    }

    [Fact]
    public async Task There_is_no_way_to_close_a_single_line_of_a_day()
    {
        var (client, order, l1, _, _) = await TwoLineOrderAsync();

        // Endpoint chốt riêng một dây chuyền đã bị gỡ: Xuất hàng chỉ có ở mức ngày.
        var response = await client.PostAsync(
            $"/api/v1/orders/{order.Id}/production-days/{Today.AddDays(-1):yyyy-MM-dd}/lines/{l1.Id}/close", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.All(await GetCellsAsync(client, order.Id), c => Assert.NotEqual("Closed", c.DayStatus));
    }

    [Fact]
    public async Task The_matrix_reports_allocation_plan_and_actual_per_line()
    {
        var (client, order, l1, l2, _) = await TwoLineOrderAsync();

        (await PostEntryAsync(client, order.Id, Today.AddDays(-1), l1.Id, 70)).EnsureSuccessStatusCode();

        var matrix = await GetMatrixAsync(client, order.Id);

        var line1 = matrix.ProductionLines.Single(l => l.Id == l1.Id);
        Assert.Equal(200, line1.AllocatedQuantity);
        Assert.Equal(200, line1.CurrentPlanQuantity);
        Assert.Equal(70, line1.ActualQuantity);

        var line2 = matrix.ProductionLines.Single(l => l.Id == l2.Id);
        Assert.Equal(200, line2.AllocatedQuantity);
        Assert.Equal(0, line2.ActualQuantity);
    }

    [Fact]
    public async Task A_shortage_cannot_be_absorbed_by_a_different_line()
    {
        var (client, order, l1, l2, cells) = await TwoLineOrderAsync();
        var yesterday = Today.AddDays(-1);

        await RecordAndCloseAsync(client, order.Id, yesterday, l1.Id, 90);

        var source = Cell(cells, yesterday, l1.Id);
        var foreignTarget = Cell(cells, Today, l2.Id);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/production-plans/{source.Id}/adjustments", new
            {
                adjustmentType = "Manual",
                shortageQuantity = 10,
                targets = new[] { new { productionPlanId = foreignTarget.Id, addOnQuantity = 10 } },
            });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("ADJUSTMENT_TARGET_LINE_MISMATCH", (await response.ReadErrorAsync()).Code);

        // Không kế hoạch nào bị đụng tới.
        Assert.All(await GetCellsAsync(client, order.Id), c => Assert.Equal(0, c.AddOnQuantity));
    }

    [Fact]
    public async Task An_automatic_proposal_only_ever_lists_cells_of_the_source_line()
    {
        var (client, order, l1, l2, cells) = await TwoLineOrderAsync();
        var yesterday = Today.AddDays(-1);

        await RecordAndCloseAsync(client, order.Id, yesterday, l1.Id, 90);

        var source = Cell(cells, yesterday, l1.Id);
        var response = await client.PostAsJsonAsync(
            $"/api/v1/production-plans/{source.Id}/adjustments/preview", new { adjustmentType = "Automatic" });

        response.EnsureSuccessStatusCode();
        var preview = await response.ReadAsync<AdjustmentPreviewResponse>();

        Assert.Equal(l1.Id, preview.ProductionLineId);
        Assert.Equal(l1.Code, preview.ProductionLineCode);
        // Chỉ còn đúng một ô hợp lệ của dây chuyền 1 (hôm nay); ô của dây chuyền 2 không được xét.
        var item = Assert.Single(preview.Items);
        Assert.Equal(l1.Id, item.ProductionLineId);
        Assert.Equal(Today, item.ProductionDate);
        Assert.Equal(10, item.AddOnQuantity);
    }

    [Fact]
    public async Task A_manual_preview_rejects_a_target_on_another_line()
    {
        var (client, order, l1, l2, cells) = await TwoLineOrderAsync();
        var yesterday = Today.AddDays(-1);

        await RecordAndCloseAsync(client, order.Id, yesterday, l1.Id, 90);

        var source = Cell(cells, yesterday, l1.Id);
        var foreignTarget = Cell(cells, Today, l2.Id);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/production-plans/{source.Id}/adjustments/preview", new
            {
                adjustmentType = "Manual",
                targets = new[] { new { productionPlanId = foreignTarget.Id, addOnQuantity = 10 } },
            });

        response.EnsureSuccessStatusCode();
        var preview = await response.ReadAsync<AdjustmentPreviewResponse>();

        // Preview không ném lỗi, nó báo lý do để UI hiển thị ngay cạnh ô người dùng vừa chọn.
        Assert.False(preview.Valid);
        Assert.Equal("ADJUSTMENT_TARGET_LINE_MISMATCH", preview.ValidationCode);
        Assert.Empty(preview.Items);
    }

    [Fact]
    public async Task The_order_completes_only_when_every_line_has_delivered()
    {
        var (client, order, l1, l2, _) = await TwoLineOrderAsync();
        var yesterday = Today.AddDays(-1);

        (await PostEntryAsync(client, order.Id, yesterday, l1.Id, 100)).EnsureSuccessStatusCode();
        (await PostEntryAsync(client, order.Id, yesterday, l2.Id, 100)).EnsureSuccessStatusCode();
        (await CloseDayAsync(client, order.Id, yesterday)).EnsureSuccessStatusCode();

        (await PostEntryAsync(client, order.Id, Today, l1.Id, 100)).EnsureSuccessStatusCode();
        (await PostEntryAsync(client, order.Id, Today, l2.Id, 100)).EnsureSuccessStatusCode();

        // Đủ 400 nhưng hôm nay chưa Xuất hàng: trạng thái chỉ được đánh giá lúc chốt sổ (CR-01 OV-4).
        Assert.Equal("Incomplete", (await GetOrderAsync(client, order.Id)).Status);

        (await CloseDayAsync(client, order.Id, Today)).EnsureSuccessStatusCode();

        var completed = await GetOrderAsync(client, order.Id);
        Assert.Equal("Completed", completed.Status);
        Assert.Equal(400, completed.TotalActual);
        Assert.Equal(0, completed.Remaining);
    }

    [Fact]
    public async Task The_order_total_is_still_capped_across_lines()
    {
        var (client, order, l1, l2, _) = await TwoLineOrderAsync();
        var yesterday = Today.AddDays(-1);

        // Ghi đủ 400 qua ba ô rồi thử nhập thêm ở ô thứ tư — trần đơn hàng cắt ngang mọi dây chuyền.
        (await PostEntryAsync(client, order.Id, yesterday, l1.Id, 100)).EnsureSuccessStatusCode();
        (await PostEntryAsync(client, order.Id, yesterday, l2.Id, 100)).EnsureSuccessStatusCode();
        (await PostEntryAsync(client, order.Id, Today, l1.Id, 100)).EnsureSuccessStatusCode();
        (await PostEntryAsync(client, order.Id, Today, l2.Id, 100)).EnsureSuccessStatusCode();

        var response = await PostEntryAsync(client, order.Id, Today, l2.Id, 1);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var error = await response.ReadErrorAsync();
        Assert.Contains(error.Code, new[] { "ACTUAL_EXCEEDS_ORDER_QUANTITY", "ENTRY_EXCEEDS_DAILY_PLAN" });

        Assert.Equal(400, (await GetOrderAsync(client, order.Id)).TotalActual);
    }

    [Fact]
    public async Task Statistics_break_the_order_down_by_production_line()
    {
        var (client, order, l1, l2, _) = await TwoLineOrderAsync();
        var yesterday = Today.AddDays(-1);

        (await PostEntryAsync(client, order.Id, yesterday, l1.Id, 90)).EnsureSuccessStatusCode();
        (await PostEntryAsync(client, order.Id, yesterday, l2.Id, 100)).EnsureSuccessStatusCode();
        (await CloseDayAsync(client, order.Id, yesterday)).EnsureSuccessStatusCode();

        var response = await client.GetAsync($"/api/v1/orders/{order.Id}/statistics");
        response.EnsureSuccessStatusCode();
        var stats = await response.ReadAsync<OrderStatisticsTestResponse>();

        var line1 = stats.ByProductionLine.Single(l => l.ProductionLineId == l1.Id);
        Assert.Equal(200, line1.AllocatedQuantity);
        Assert.Equal(90, line1.TotalActual);
        Assert.Equal(10, line1.Shortage);

        var line2 = stats.ByProductionLine.Single(l => l.ProductionLineId == l2.Id);
        Assert.Equal(100, line2.TotalActual);
        Assert.Equal(0, line2.Shortage);
    }

    [Fact]
    public async Task Concurrent_entries_on_two_lines_can_never_exceed_the_order_quantity()
    {
        var client = await ClientAsync();
        var order = await ReceiveOrderAsync(client, 100);
        var l1 = await CreateLineAsync(client, 1);
        var l2 = await CreateLineAsync(client, 2);

        // Mỗi dây chuyền được phân 50 nhưng kế hoạch ngày cho phép 80 mỗi bên sau khi... không:
        // giữ đúng 50/50, rồi bắn hai request 50 cùng lúc — tổng vừa khít, không được vượt.
        (await PostScheduleAsync(client, order.Id, Today, Today,
            [(l1.Id, 50, [50]), (l2.Id, 50, [50])])).EnsureSuccessStatusCode();

        var responses = await Task.WhenAll(
            PostEntryAsync(client, order.Id, Today, l1.Id, 50),
            PostEntryAsync(client, order.Id, Today, l2.Id, 50));

        Assert.All(responses, r => Assert.True(r.IsSuccessStatusCode));

        var updated = await GetOrderAsync(client, order.Id);
        Assert.Equal(100, updated.TotalActual);
        Assert.True(updated.TotalActual <= updated.Quantity);

        // Một đơn vị nữa là vượt, dù ô nào cũng vậy.
        var extra = await PostEntryAsync(client, order.Id, Today, l1.Id, 1);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, extra.StatusCode);
    }
}

/// <summary>Chỉ khai báo phần thống kê mà test này cần đọc.</summary>
public sealed record OrderLineStatisticsTestResponse(
    Guid ProductionLineId, string ProductionLineCode, int AllocatedQuantity,
    int TotalPlan, int TotalActual, int Shortage);

public sealed record OrderStatisticsTestResponse(
    Guid OrderId, string ShoeCode, int TotalActual,
    IReadOnlyList<OrderLineStatisticsTestResponse> ByProductionLine);
