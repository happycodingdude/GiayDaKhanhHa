using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace ProductionManagement.IntegrationTests;

public class AdjustmentApiTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    /// <summary>Đơn hàng thiếu 20 đơn vị ở ngày đầu tiên.</summary>
    private async Task<(HttpClient Client, OrderResponse Order, IReadOnlyList<ProductionDayResponse> Days)>
        OrderWithShortageAsync(params int[] plan)
    {
        var client = await ClientAsync();
        var (order, days) = await CreateOrderAsync(client, plan.Length > 0 ? plan : [100, 120, 200, 250]);

        (await PostActualAsync(client, order.Id, days[0].ProductionDate, days[0].PlannedQuantity - 20))
            .EnsureSuccessStatusCode();

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
        (await PostActualAsync(client, order.Id, days[0].ProductionDate, 100)).EnsureSuccessStatusCode();

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

        // Quản lý sửa lại thực tế, nên phần thiếu không còn là 20.
        var record = days[0].ProductionRecordId!.Value;
        (await PutActualAsync(client, order.Id, record, days[0].PlannedQuantity - 10)).EnsureSuccessStatusCode();

        var response = await ApplyAsync(client, days[0].Id, "Manual", 20, (days[1].Id, 20));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("ADJUSTMENT_OUTDATED", (await response.ReadErrorAsync()).Code);
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
    public async Task Correcting_the_actual_down_grows_a_manual_add_on_to_the_new_shortage()
    {
        var (client, order, days) = await OrderWithShortageAsync();

        (await ApplyAsync(client, days[0].Id, "Manual", 20, (days[2].Id, 20))).EnsureSuccessStatusCode();

        // Quản lý sửa thực tế vào cuối ngày: phần thiếu giờ là 30 chứ không phải 20.
        var response = await PutActualAsync(
            client, order.Id, days[0].ProductionRecordId!.Value, days[0].PlannedQuantity - 30);
        response.EnsureSuccessStatusCode();

        var recalculation = (await response.ReadAsync<ProductionRecordResponse>()).AdjustmentRecalculation;
        Assert.NotNull(recalculation);
        Assert.Equal("Recalculated", recalculation.Outcome);
        Assert.Equal(20, recalculation.PreviousShortageQuantity);
        Assert.Equal(30, recalculation.ShortageQuantity);

        // Ngày quản lý đã chọn vẫn giữ khoản bù, giờ theo số lượng đã sửa.
        var after = await GetDaysAsync(client, order.Id);
        Assert.Equal(30, after[2].AddOnQuantity);
        Assert.Equal(days[2].PlannedQuantity + 30, after[2].PlannedQuantity);
        Assert.Equal(0, after[1].AddOnQuantity);
        Assert.True(after[0].HasActiveAdjustment);

        // Điều chỉnh đã cũ bị hoàn tác chứ không bị sửa, nên cả hai bản ghi đều còn hiển thị.
        var history = await HistoryAsync(client, order.Id);
        Assert.Equal(2, history.Count);
        Assert.Single(history, a => a.Status == "Applied" && a.ShortageQuantity == 30);
        Assert.Single(history, a => a.Status == "Reversed" && a.ShortageQuantity == 20);
    }

    [Fact]
    public async Task Correcting_the_actual_splits_an_automatic_add_on_evenly_again()
    {
        var (client, order, days) = await OrderWithShortageAsync(100, 120, 200, 250);

        // Chia 20 cho ba ngày còn lại ra 7 / 7 / 6.
        (await ApplyAsync(client, days[0].Id, "Automatic", 20, (days[1].Id, 7), (days[2].Id, 7), (days[3].Id, 6)))
            .EnsureSuccessStatusCode();

        (await PutActualAsync(client, order.Id, days[0].ProductionRecordId!.Value, days[0].PlannedQuantity - 30))
            .EnsureSuccessStatusCode();

        // Chia 30 cho đúng ba ngày đó ra chẵn 10 mỗi ngày.
        var after = await GetDaysAsync(client, order.Id);
        Assert.Equal([0, 10, 10, 10], after.Select(day => day.AddOnQuantity));
        Assert.Equal(
            days.Skip(1).Select(day => day.PlannedQuantity + 10),
            after.Skip(1).Select(day => day.PlannedQuantity));

        var applied = Assert.Single(await HistoryAsync(client, order.Id), a => a.Status == "Applied");
        Assert.Equal("Automatic", applied.AdjustmentType);
        Assert.Equal(30, applied.ShortageQuantity);
    }

    [Fact]
    public async Task Correcting_the_actual_up_to_the_plan_removes_the_add_on_entirely()
    {
        var (client, order, days) = await OrderWithShortageAsync();

        (await ApplyAsync(client, days[0].Id, "Manual", 20, (days[1].Id, 20))).EnsureSuccessStatusCode();

        var response = await PutActualAsync(
            client, order.Id, days[0].ProductionRecordId!.Value, days[0].PlannedQuantity);
        response.EnsureSuccessStatusCode();

        var recalculation = (await response.ReadAsync<ProductionRecordResponse>()).AdjustmentRecalculation;
        Assert.NotNull(recalculation);
        Assert.Equal("Removed", recalculation.Outcome);
        Assert.Equal(0, recalculation.ShortageQuantity);

        // Không còn phần thiếu nào, nên ngày đích quay về đúng kế hoạch của nó.
        var after = await GetDaysAsync(client, order.Id);
        Assert.Equal(0, after[1].AddOnQuantity);
        Assert.Equal(days[1].PlannedQuantity, after[1].PlannedQuantity);
        Assert.False(after[0].HasActiveAdjustment);

        var history = await HistoryAsync(client, order.Id);
        Assert.Equal("Reversed", Assert.Single(history).Status);
    }

    [Fact]
    public async Task Re_entering_the_same_actual_leaves_the_adjustment_untouched()
    {
        var (client, order, days) = await OrderWithShortageAsync();

        var applied = await (await ApplyAsync(client, days[0].Id, "Manual", 20, (days[1].Id, 20)))
            .ReadAsync<PlanAdjustmentResponse>();

        var response = await PutActualAsync(
            client, order.Id, days[0].ProductionRecordId!.Value, days[0].ActualQuantity!.Value);
        response.EnsureSuccessStatusCode();

        // Phần thiếu không đổi, nên lịch sử không bị làm nhiễu bằng một lượt hoàn tác + áp dụng lại.
        Assert.Null((await response.ReadAsync<ProductionRecordResponse>()).AdjustmentRecalculation);

        var history = await HistoryAsync(client, order.Id);
        Assert.Equal(applied.Id, Assert.Single(history).Id);
        Assert.Equal("Applied", history[0].Status);
        Assert.Equal(20, (await GetDaysAsync(client, order.Id))[1].AddOnQuantity);
    }

    [Fact]
    public async Task A_shortage_on_the_final_production_day_has_nowhere_to_go()
    {
        var client = await ClientAsync();
        // Kỳ sản xuất kết thúc hôm nay, nên phần thiếu rơi vào ngày cuối và không còn ngày nào sau đó.
        var (order, days) = await CreateOrderFromAsync(client, Today.AddDays(-1), 100, 100);
        (await PostActualAsync(client, order.Id, days[1].ProductionDate, 80)).EnsureSuccessStatusCode();

        var response = await PreviewAsync(client, days[1].Id, "Automatic");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("NO_ELIGIBLE_TARGET_PLANS", (await response.ReadErrorAsync()).Code);
    }
}
