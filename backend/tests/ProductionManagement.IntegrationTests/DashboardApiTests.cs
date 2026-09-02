using System.Net.Http.Json;
using Xunit;

namespace ProductionManagement.IntegrationTests;

/// <summary>Các khối mới của dashboard — CR-01 §6.9, §14.5, AC-19.</summary>
public class DashboardApiTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    private sealed record TodayProduction(Guid OrderId, string OrderCode, int PlannedQuantity, int DayActualQuantity);

    private sealed record UnclosedDay(Guid OrderId, DateOnly ProductionDate, int PlannedQuantity, int DayActualQuantity);

    private sealed record OpenShortage(Guid OrderId, DateOnly ProductionDate, int ShortageQuantity);

    private sealed record TrackedOrderDay(
        DateOnly ProductionDate, int PlannedQuantity, int? ActualQuantity, string DayStatus);

    private sealed record TrackedOrder(
        Guid OrderId,
        bool TodayHasPlan,
        int TodayPlannedQuantity,
        int TodayActualQuantity,
        string? TodayStatus,
        int? TodayDifference,
        IReadOnlyList<TrackedOrderDay> Days);

    private sealed record Dashboard(
        IReadOnlyList<TodayProduction> TodayProduction,
        IReadOnlyList<UnclosedDay> UnclosedPastDays,
        IReadOnlyList<OpenShortage> OpenShortages,
        IReadOnlyList<TrackedOrder> TrackedOrders);

    private static async Task<Dashboard> DashboardAsync(HttpClient client)
        => await (await client.GetAsync("/api/v1/statistics/dashboard")).ReadAsync<Dashboard>();

    [Fact]
    public async Task A_past_day_that_was_never_started_still_shows_as_unclosed()
    {
        var client = await ClientAsync();
        // CR-01 §14.5: nguồn dữ liệu là production_plans, không phải production_days — ngày quá khứ
        // hoàn toàn không nhập gì thì chưa có dòng production_days nào, mà đó lại đúng là trường
        // hợp cần cảnh báo nhất.
        var (order, days) = await CreateOrderFromAsync(client, Today.AddDays(-1), 100, 100);

        var dashboard = await DashboardAsync(client);
        var unclosed = Assert.Single(dashboard.UnclosedPastDays, d => d.OrderId == order.Id);

        Assert.Equal(days[0].ProductionDate, unclosed.ProductionDate);
        Assert.Equal(0, unclosed.DayActualQuantity);
    }

    [Fact]
    public async Task Closing_a_past_day_takes_it_off_the_unclosed_list_and_onto_the_shortage_list()
    {
        var client = await ClientAsync();
        // AC-19.
        var (order, days) = await CreateOrderFromAsync(client, Today.AddDays(-1), 100, 100);

        (await PostEntryAsync(client, order.Id, days[0].ProductionDate, 70)).EnsureSuccessStatusCode();
        Assert.Single((await DashboardAsync(client)).UnclosedPastDays, d => d.OrderId == order.Id);

        (await CloseDayAsync(client, order.Id, days[0].ProductionDate)).EnsureSuccessStatusCode();

        var dashboard = await DashboardAsync(client);
        Assert.DoesNotContain(dashboard.UnclosedPastDays, d => d.OrderId == order.Id);

        var shortage = Assert.Single(dashboard.OpenShortages, s => s.OrderId == order.Id);
        Assert.Equal(30, shortage.ShortageQuantity);
    }

    [Fact]
    public async Task An_open_day_today_appears_under_today_production_and_has_no_shortage()
    {
        var client = await ClientAsync();
        var (order, days) = await CreateOrderAsync(client, 200);

        (await PostEntryAsync(client, order.Id, days[0].ProductionDate, 60)).EnsureSuccessStatusCode();

        var dashboard = await DashboardAsync(client);
        var today = Assert.Single(dashboard.TodayProduction, t => t.OrderId == order.Id);

        Assert.Equal(200, today.PlannedQuantity);
        Assert.Equal(60, today.DayActualQuantity);
        // Ngày còn mở không bao giờ sinh phần thiếu (CR-01 N-07).
        Assert.DoesNotContain(dashboard.OpenShortages, s => s.OrderId == order.Id);
    }

    [Fact]
    public async Task An_open_day_with_entries_carries_its_provisional_quantity_into_the_timeline()
    {
        var client = await ClientAsync();
        // Hồi quy: timeline từng chỉ chấm điểm ngày đã Xuất hàng, nên sản lượng đã ghi nhận của
        // ngày đang sản xuất biến mất khỏi dashboard.
        var (order, days) = await CreateOrderAsync(client, 100, 100);

        (await PostEntryAsync(client, order.Id, days[0].ProductionDate, 20)).EnsureSuccessStatusCode();

        var tracked = Assert.Single(
            (await DashboardAsync(client)).TrackedOrders, o => o.OrderId == order.Id);

        Assert.True(tracked.TodayHasPlan);
        Assert.Equal(100, tracked.TodayPlannedQuantity);
        Assert.Equal(20, tracked.TodayActualQuantity);
        Assert.Equal("InProduction", tracked.TodayStatus);
        // Chênh lệch vẫn null: ngày chưa chốt sổ thì chưa có con số chính thức để so (CR-01 N-07).
        Assert.Null(tracked.TodayDifference);

        var timelineDay = Assert.Single(tracked.Days, d => d.ProductionDate == days[0].ProductionDate);
        Assert.Equal(20, timelineDay.ActualQuantity);
        Assert.Equal("InProduction", timelineDay.DayStatus);
    }

    [Fact]
    public async Task A_closed_day_reports_the_difference_for_today()
    {
        var client = await ClientAsync();
        var (order, days) = await CreateOrderAsync(client, 100, 100);

        await RecordAndCloseAsync(client, order.Id, days[0].ProductionDate, 80);

        var tracked = Assert.Single(
            (await DashboardAsync(client)).TrackedOrders, o => o.OrderId == order.Id);

        Assert.Equal("Closed", tracked.TodayStatus);
        Assert.Equal(80, tracked.TodayActualQuantity);
        Assert.Equal(-20, tracked.TodayDifference);
    }

    [Fact]
    public async Task A_handled_shortage_leaves_the_open_shortage_list()
    {
        var client = await ClientAsync();
        var (order, days) = await CreateOrderAsync(client, 100, 120);

        await RecordAndCloseAsync(client, order.Id, days[0].ProductionDate, 80);
        Assert.Single((await DashboardAsync(client)).OpenShortages, s => s.OrderId == order.Id);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/production-plans/{days[0].Id}/adjustments",
            new
            {
                adjustmentType = "Manual",
                shortageQuantity = 20,
                targets = new[] { new { productionPlanId = days[1].Id, addOnQuantity = 20 } },
            });
        response.EnsureSuccessStatusCode();

        Assert.DoesNotContain((await DashboardAsync(client)).OpenShortages, s => s.OrderId == order.Id);
    }
}
