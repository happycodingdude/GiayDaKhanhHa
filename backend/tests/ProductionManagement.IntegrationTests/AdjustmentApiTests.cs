using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace ProductionManagement.IntegrationTests;

public class AdjustmentApiTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    /// <summary>
    /// Đơn hàng thiếu 20 đơn vị ở ngày đầu tiên, ngày đó ĐÃ Xuất hàng. Sau CR-01 phần thiếu chỉ tồn
    /// tại ở ngày đã chốt sổ, nên mọi test về điều chỉnh đều phải đóng ngày nguồn trước.
    /// </summary>
    private async Task<(HttpClient Client, OrderResponse Order, IReadOnlyList<ProductionDayResponse> Days)>
        OrderWithShortageAsync(params int[] plan)
    {
        var client = await ClientAsync();
        var (order, days) = await CreateOrderAsync(client, plan.Length > 0 ? plan : [100, 120, 200, 250]);

        await RecordAndCloseAsync(client, order.Id, days[0].ProductionDate, days[0].PlannedQuantity - 20);

        return (client, order, await GetDaysAsync(client, order.Id));
    }

    private static Task<HttpResponseMessage> PreviewAsync(
        HttpClient client, Guid planId, string type, params (Guid PlanId, int AddOn)[] targets)
        => client.PostAsJsonAsync($"/api/v1/production-plans/{planId}/adjustments/preview", new
        {
            adjustmentType = type,
            targets = targets.Select(t => new { productionPlanId = t.PlanId, addOnQuantity = t.AddOn }).ToArray(),
        });

    private static Task<HttpResponseMessage> ApplyAsync(
        HttpClient client, Guid planId, string type, int shortage, params (Guid PlanId, int AddOn)[] targets)
        => client.PostAsJsonAsync($"/api/v1/production-plans/{planId}/adjustments", new
        {
            adjustmentType = type,
            shortageQuantity = shortage,
            targets = targets.Select(t => new { productionPlanId = t.PlanId, addOnQuantity = t.AddOn }).ToArray(),
        });

    [Fact]
    public async Task A_manual_preview_returns_the_proposal_without_changing_any_plan()
    {
        var (client, order, days) = await OrderWithShortageAsync();

        var response = await PreviewAsync(client, days[0].Id, "Manual", (days[1].Id, 20));
        response.EnsureSuccessStatusCode();

        var preview = await response.ReadAsync<AdjustmentPreviewResponse>();
        Assert.True(preview.Valid);
        Assert.Equal(20, preview.ShortageQuantity);
        Assert.Equal(20, preview.TotalAddOnQuantity);
        Assert.Equal(days[1].PlannedQuantity + 20, preview.Items[0].PlannedQuantityAfter);

        // Preview không bao giờ lưu xuống: các kế hoạch đã lưu không đổi.
        var after = await GetDaysAsync(client, order.Id);
        Assert.Equal(days.Select(d => d.PlannedQuantity), after.Select(d => d.PlannedQuantity));
        Assert.Empty(await (await client.GetAsync($"/api/v1/orders/{order.Id}/plan-adjustments"))
            .ReadAsync<List<PlanAdjustmentResponse>>());
    }

    [Fact]
    public async Task An_automatic_preview_spreads_the_shortage_over_every_remaining_day()
    {
        var (client, _, days) = await OrderWithShortageAsync(100, 120, 200, 250);

        var response = await PreviewAsync(client, days[0].Id, "Automatic");
        response.EnsureSuccessStatusCode();

        var preview = await response.ReadAsync<AdjustmentPreviewResponse>();
        Assert.Equal(3, preview.Items.Count);
        Assert.Equal(20, preview.TotalAddOnQuantity);
        Assert.Equal([7, 7, 6], preview.Items.Select(i => i.AddOnQuantity));
        // Ngày nguồn không bao giờ được nhận khoản bù.
        Assert.DoesNotContain(preview.Items, item => item.ProductionPlanId == days[0].Id);
    }

    [Fact]
    public async Task A_manual_proposal_whose_total_differs_from_the_shortage_previews_as_invalid()
    {
        var (client, _, days) = await OrderWithShortageAsync();

        var response = await PreviewAsync(client, days[0].Id, "Manual", (days[1].Id, 5));
        response.EnsureSuccessStatusCode();

        var preview = await response.ReadAsync<AdjustmentPreviewResponse>();
        Assert.False(preview.Valid);
        Assert.Equal("ADJUSTMENT_TOTAL_MISMATCH", preview.ValidationCode);
    }

    [Fact]
    public async Task Previewing_a_day_without_a_shortage_is_rejected()
    {
        var client = await ClientAsync();
        var (order, days) = await CreateOrderAsync(client, 100, 100);
        await RecordAndCloseAsync(client, order.Id, days[0].ProductionDate, 100);

        var response = await PreviewAsync(client, days[0].Id, "Automatic");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("NO_SHORTAGE", (await response.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task Applying_increases_only_the_target_plans_and_leaves_the_order_quantity_alone()
    {
        var (client, order, days) = await OrderWithShortageAsync();

        var response = await ApplyAsync(client, days[0].Id, "Manual", 20, (days[2].Id, 20));
        response.EnsureSuccessStatusCode();

        var after = await GetDaysAsync(client, order.Id);
        Assert.Equal(days[2].PlannedQuantity + 20, after[2].PlannedQuantity);
        Assert.Equal(20, after[2].AddOnQuantity);
        // Kế hoạch ban đầu là bất biến và không ngày nào khác bị giảm.
        Assert.Equal(days[2].InitialPlannedQuantity, after[2].InitialPlannedQuantity);
        Assert.Equal(days[0].PlannedQuantity, after[0].PlannedQuantity);
        Assert.Equal(days[1].PlannedQuantity, after[1].PlannedQuantity);
        Assert.Equal(days[3].PlannedQuantity, after[3].PlannedQuantity);

        var updatedOrder = await GetOrderAsync(client, order.Id);
        Assert.Equal(order.Quantity, updatedOrder.Quantity);
        // Tổng kế hoạch giờ có thể vượt số lượng đơn hàng. Đó là chủ đích.
        Assert.Equal(order.TotalPlan + 20, updatedOrder.TotalPlan);
        Assert.Equal(order.Quantity, updatedOrder.TotalInitialPlan);
    }

    [Fact]
    public async Task A_source_day_can_only_have_one_applied_adjustment_at_a_time()
    {
        var (client, _, days) = await OrderWithShortageAsync();

        (await ApplyAsync(client, days[0].Id, "Manual", 20, (days[1].Id, 20))).EnsureSuccessStatusCode();

        var second = await ApplyAsync(client, days[0].Id, "Manual", 20, (days[2].Id, 20));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("ACTIVE_ADJUSTMENT_EXISTS", (await second.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task A_stale_proposal_is_rejected_rather_than_silently_adjusted()
    {
        var (client, order, days) = await OrderWithShortageAsync();

        // Phần thiếu thật là 20; client gửi lên một con số đã cũ. Server tính lại từ trạng thái
        // sống chứ không bao giờ tin preview (Step 4 §10).
        var response = await ApplyAsync(client, days[0].Id, "Manual", 10, (days[1].Id, 10));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("ADJUSTMENT_OUTDATED", (await response.ReadErrorAsync()).Code);
        _ = order;
    }

    [Fact]
    public async Task A_target_that_is_not_a_later_production_day_is_rejected()
    {
        var (client, _, days) = await OrderWithShortageAsync();

        // Bản thân ngày nguồn không bao giờ là ngày đích hợp lệ.
        var response = await ApplyAsync(client, days[0].Id, "Manual", 20, (days[0].Id, 20));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("INVALID_ADJUSTMENT_TARGET", (await response.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task A_duplicate_target_within_one_adjustment_is_rejected()
    {
        var (client, _, days) = await OrderWithShortageAsync();

        var response = await ApplyAsync(client, days[0].Id, "Manual", 20, (days[1].Id, 10), (days[1].Id, 10));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("DUPLICATE_ADJUSTMENT_TARGET", (await response.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task An_automatic_apply_that_does_not_match_the_current_allocation_is_rejected()
    {
        var (client, _, days) = await OrderWithShortageAsync(100, 120, 200, 250);

        // Một cách chia tự đặt tay nhưng gửi lên dạng Automatic sẽ không khớp đề xuất của hệ thống.
        var response = await ApplyAsync(client, days[0].Id, "Automatic", 20, (days[1].Id, 20));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("ADJUSTMENT_OUTDATED", (await response.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task Reversing_removes_the_add_on_and_keeps_the_history()
    {
        var (client, order, days) = await OrderWithShortageAsync();

        var applied = await (await ApplyAsync(client, days[0].Id, "Manual", 20, (days[1].Id, 20)))
            .ReadAsync<PlanAdjustmentResponse>();

        var reverse = await client.PostAsync($"/api/v1/plan-adjustments/{applied.Id}/reverse", null);
        reverse.EnsureSuccessStatusCode();

        var reversed = await reverse.ReadAsync<PlanAdjustmentResponse>();
        Assert.Equal("Reversed", reversed.Status);
        Assert.NotNull(reversed.ReversedBy);
        // Các dòng lịch sử được giữ nguyên.
        Assert.Equal(20, reversed.Items.Sum(i => i.AddOnQuantity));

        var after = await GetDaysAsync(client, order.Id);
        Assert.Equal(days[1].PlannedQuantity, after[1].PlannedQuantity);
        Assert.Equal(0, after[1].AddOnQuantity);

        var history = await (await client.GetAsync($"/api/v1/orders/{order.Id}/plan-adjustments"))
            .ReadAsync<List<PlanAdjustmentResponse>>();
        Assert.Single(history);
        Assert.Equal("Reversed", history[0].Status);
    }

    [Fact]
    public async Task An_adjustment_cannot_be_reversed_twice()
    {
        var (client, _, days) = await OrderWithShortageAsync();

        var applied = await (await ApplyAsync(client, days[0].Id, "Manual", 20, (days[1].Id, 20)))
            .ReadAsync<PlanAdjustmentResponse>();

        (await client.PostAsync($"/api/v1/plan-adjustments/{applied.Id}/reverse", null)).EnsureSuccessStatusCode();
        var second = await client.PostAsync($"/api/v1/plan-adjustments/{applied.Id}/reverse", null);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("ADJUSTMENT_NOT_APPLIED", (await second.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task A_new_adjustment_can_be_created_after_the_previous_one_is_reversed()
    {
        var (client, order, days) = await OrderWithShortageAsync();

        var first = await (await ApplyAsync(client, days[0].Id, "Manual", 20, (days[1].Id, 20)))
            .ReadAsync<PlanAdjustmentResponse>();
        (await client.PostAsync($"/api/v1/plan-adjustments/{first.Id}/reverse", null)).EnsureSuccessStatusCode();

        var second = await ApplyAsync(client, days[0].Id, "Manual", 20, (days[2].Id, 20));
        second.EnsureSuccessStatusCode();

        // Cả hai điều chỉnh đều còn trong lịch sử; cái cũ không bị sửa.
        var history = await (await client.GetAsync($"/api/v1/orders/{order.Id}/plan-adjustments"))
            .ReadAsync<List<PlanAdjustmentResponse>>();
        Assert.Equal(2, history.Count);
        Assert.Single(history, item => item.Status == "Applied");
        Assert.Single(history, item => item.Status == "Reversed");
    }

    [Fact]
    public async Task Applying_marks_the_source_day_as_having_an_active_adjustment()
    {
        var (client, order, days) = await OrderWithShortageAsync();

        var applied = await (await ApplyAsync(client, days[0].Id, "Manual", 20, (days[1].Id, 20)))
            .ReadAsync<PlanAdjustmentResponse>();

        var after = await GetDaysAsync(client, order.Id);
        Assert.True(after[0].HasActiveAdjustment);
        Assert.Equal(applied.Id, after[0].ActiveAdjustmentId);
        Assert.False(after[1].HasActiveAdjustment);
    }

    private static async Task<IReadOnlyList<PlanAdjustmentResponse>> HistoryAsync(HttpClient client, Guid orderId)
        => await (await client.GetAsync($"/api/v1/orders/{orderId}/plan-adjustments"))
            .ReadAsync<List<PlanAdjustmentResponse>>();

    [Fact]
    public async Task A_shortage_on_the_final_production_day_has_nowhere_to_go()
    {
        var client = await ClientAsync();
        // Kỳ sản xuất kết thúc hôm nay, nên phần thiếu rơi vào ngày cuối và không còn ngày nào sau đó.
        var (order, days) = await CreateOrderFromAsync(client, Today.AddDays(-1), 100, 100);
        await RecordAndCloseAsync(client, order.Id, days[1].ProductionDate, 80);

        var response = await PreviewAsync(client, days[1].Id, "Automatic");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("NO_ELIGIBLE_TARGET_DAY", (await response.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task A_source_day_that_is_still_open_has_no_shortage_to_handle()
    {
        var client = await ClientAsync();
        // AC-13. Ngày còn mở chưa có con số chính thức nào, nên chưa có gì để bù (CR-01 OV-5).
        var (order, days) = await CreateOrderAsync(client, 100, 120);
        (await PostEntryAsync(client, order.Id, days[0].ProductionDate, 80)).EnsureSuccessStatusCode();

        var response = await PreviewAsync(client, days[0].Id, "Automatic");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("SOURCE_DAY_NOT_CLOSED", (await response.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task A_target_day_that_is_already_closed_cannot_receive_an_add_on()
    {
        var client = await ClientAsync();
        // AC-14. Ngày 0 và ngày 1 đều là quá khứ/hôm nay nên cả hai đóng được.
        var (order, days) = await CreateOrderFromAsync(client, Today.AddDays(-1), 100, 120, 200);

        await RecordAndCloseAsync(client, order.Id, days[1].ProductionDate, 100);
        await RecordAndCloseAsync(client, order.Id, days[0].ProductionDate, 80);

        var response = await ApplyAsync(client, days[0].Id, "Manual", 20, (days[1].Id, 20));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("TARGET_DAY_CLOSED", (await response.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task Automatic_allocation_skips_days_that_have_already_been_closed()
    {
        var client = await ClientAsync();
        var (order, days) = await CreateOrderFromAsync(client, Today.AddDays(-1), 100, 120, 200, 250);

        // Ngày 1 là hôm nay và đã được chốt sổ, nên nó rơi khỏi tập ngày ứng viên.
        await RecordAndCloseAsync(client, order.Id, days[1].ProductionDate, 120);
        await RecordAndCloseAsync(client, order.Id, days[0].ProductionDate, 80);

        var preview = await (await PreviewAsync(client, days[0].Id, "Automatic"))
            .ReadAsync<AdjustmentPreviewResponse>();

        Assert.Equal([days[2].Id, days[3].Id], preview.Items.Select(i => i.ProductionPlanId));
        Assert.Equal(20, preview.TotalAddOnQuantity);
    }

    [Fact]
    public async Task An_add_on_raises_the_recording_allowance_of_the_target_day()
    {
        var client = await ClientAsync();
        // AC-16 / N-12: bù 20 vào ngày kế hoạch 120 thì trần nhập của ngày đó tăng theo.
        var (order, days) = await CreateOrderFromAsync(client, Today.AddDays(-1), 100, 120);

        await RecordAndCloseAsync(client, order.Id, days[0].ProductionDate, 80);
        (await ApplyAsync(client, days[0].Id, "Manual", 20, (days[1].Id, 20))).EnsureSuccessStatusCode();

        var day = await (await GetDayAsync(client, order.Id, days[1].ProductionDate))
            .ReadAsync<ProductionDayDetailResponse>();

        Assert.Equal(140, day.PlannedQuantity);
        Assert.Equal(20, day.AddOnQuantity);
        Assert.Equal(140, day.RemainingAllowance);
        (await PostEntryAsync(client, order.Id, days[1].ProductionDate, 140)).EnsureSuccessStatusCode();
    }
}
