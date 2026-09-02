using System.Net;
using Xunit;

namespace ProductionManagement.IntegrationTests;

/// <summary>
/// Xuất hàng — chốt sổ ngày sản xuất. CR-01 AC-07..AC-12.
/// </summary>
public class ProductionDayCloseApiTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Closing_snapshots_the_actual_and_produces_the_shortage()
    {
        var client = await ClientAsync();
        // AC-07: đóng với 160/200.
        var (order, days) = await CreateOrderAsync(client, 200, 200);

        (await PostEntryAsync(client, order.Id, days[0].ProductionDate, 100)).EnsureSuccessStatusCode();
        (await PostEntryAsync(client, order.Id, days[0].ProductionDate, 60)).EnsureSuccessStatusCode();

        var response = await CloseDayAsync(client, order.Id, days[0].ProductionDate);
        response.EnsureSuccessStatusCode();

        var closed = await response.ReadAsync<CloseProductionDayResponse>();
        Assert.Equal("Closed", closed.DayStatus);
        Assert.Equal(160, closed.ActualQuantity);
        Assert.Equal(40, closed.ShortageQuantity);
        Assert.Equal(-40, closed.Difference);
        Assert.True(closed.HasShortage);
        Assert.False(closed.OrderCompleted);

        var timeline = await GetDaysAsync(client, order.Id);
        Assert.Equal("Closed", timeline[0].DayStatus);
        Assert.False(timeline[0].IsProvisional);
        Assert.Equal(40, timeline[0].ShortageQuantity);
        Assert.NotNull(timeline[0].ClosedAt);
    }

    [Fact]
    public async Task Closing_twice_is_rejected()
    {
        var client = await ClientAsync();
        // AC-08. Đây là thao tác không hoàn tác được nên phải chặn ở backend (CR-01 §14.7).
        var (order, days) = await CreateOrderAsync(client, 100);

        (await PostEntryAsync(client, order.Id, days[0].ProductionDate, 100)).EnsureSuccessStatusCode();
        (await CloseDayAsync(client, order.Id, days[0].ProductionDate)).EnsureSuccessStatusCode();

        var second = await CloseDayAsync(client, order.Id, days[0].ProductionDate);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("DAY_ALREADY_CLOSED", (await second.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task A_closed_day_accepts_no_entry_change_at_all()
    {
        var client = await ClientAsync();
        // AC-09: ngày đã đóng là bất biến — không sửa, không xoá, không thêm.
        var (order, days) = await CreateOrderAsync(client, 200, 200);

        var created = await PostEntryAsync(client, order.Id, days[0].ProductionDate, 50);
        var entryId = (await created.ReadAsync<ProductionDayDetailResponse>()).Entries[0].Id;
        (await CloseDayAsync(client, order.Id, days[0].ProductionDate)).EnsureSuccessStatusCode();

        foreach (var response in new[]
                 {
                     await PostEntryAsync(client, order.Id, days[0].ProductionDate, 10),
                     await PutEntryAsync(client, entryId, 10),
                     await DeleteEntryAsync(client, entryId),
                 })
        {
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("DAY_ALREADY_CLOSED", (await response.ReadErrorAsync()).Code);
        }
    }

    [Fact]
    public async Task Closing_a_day_with_no_entry_is_valid()
    {
        var client = await ClientAsync();
        // AC-10: "cả ngày không sản xuất được" — hợp lệ, và không tạo entry bằng 0 nào.
        var (order, days) = await CreateOrderAsync(client, 150, 150);

        var response = await CloseDayAsync(client, order.Id, days[0].ProductionDate);
        response.EnsureSuccessStatusCode();

        var closed = await response.ReadAsync<CloseProductionDayResponse>();
        Assert.Equal(0, closed.ActualQuantity);
        Assert.Equal(150, closed.ShortageQuantity);
        Assert.True(closed.HasShortage);

        var day = await (await GetDayAsync(client, order.Id, days[0].ProductionDate))
            .ReadAsync<ProductionDayDetailResponse>();
        Assert.Empty(day.Entries);
        Assert.Equal(0, day.RemainingAllowance);
    }

    [Fact]
    public async Task The_order_completes_only_at_close_time()
    {
        var client = await ClientAsync();
        // AC-11 + AC-12: tổng đã bằng số lượng đơn nhưng chưa đóng thì đơn vẫn Incomplete.
        var (order, days) = await CreateOrderAsync(client, 100);

        (await PostEntryAsync(client, order.Id, days[0].ProductionDate, 100)).EnsureSuccessStatusCode();
        Assert.Equal("Incomplete", (await GetOrderAsync(client, order.Id)).Status);

        var response = await CloseDayAsync(client, order.Id, days[0].ProductionDate);
        var closed = await response.ReadAsync<CloseProductionDayResponse>();

        Assert.Equal("Completed", closed.OrderStatus);
        Assert.True(closed.OrderCompleted);
        Assert.False(closed.HasShortage);

        var completed = await GetOrderAsync(client, order.Id);
        Assert.Equal("Completed", completed.Status);
        Assert.Equal(0, completed.Remaining);
        Assert.Equal(100m, completed.ProgressPercentage);
    }

    [Fact]
    public async Task A_completed_order_refuses_new_entries_but_still_allows_closing_leftover_days()
    {
        var client = await ClientAsync();
        // CR-01 §14.6: nếu không cho đóng, các ngày đó nằm mãi trong unclosedPastDays và cảnh báo
        // đỏ trên dashboard không bao giờ tắt được.
        var (order, days) = await CreateOrderFromAsync(client, Today.AddDays(-1), 100, 50);

        (await PostEntryAsync(client, order.Id, days[1].ProductionDate, 50)).EnsureSuccessStatusCode();
        await RecordAndCloseAsync(client, order.Id, days[0].ProductionDate, 100);
        Assert.Equal("Completed", (await GetOrderAsync(client, order.Id)).Status);

        var rejected = await PostEntryAsync(client, order.Id, days[1].ProductionDate, 1);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Equal("ORDER_ALREADY_COMPLETED", (await rejected.ReadErrorAsync()).Code);

        var closed = await CloseDayAsync(client, order.Id, days[1].ProductionDate);
        closed.EnsureSuccessStatusCode();
        // Phần thiếu của một đơn đã hoàn thành không mở luồng Xử lý thiếu.
        Assert.False((await closed.ReadAsync<CloseProductionDayResponse>()).HasShortage);
    }

    [Fact]
    public async Task A_future_day_and_a_day_without_a_plan_cannot_be_closed()
    {
        var client = await ClientAsync();
        var (order, days) = await CreateOrderAsync(client, 100, 100, 0);

        var future = await CloseDayAsync(client, order.Id, days[1].ProductionDate);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, future.StatusCode);
        Assert.Equal("FUTURE_DATE_NOT_ALLOWED", (await future.ReadErrorAsync()).Code);

        var (zeroOrder, zeroDays) = await CreateOrderFromAsync(client, Today.AddDays(-1), 0, 100);
        var noPlan = await CloseDayAsync(client, zeroOrder.Id, zeroDays[0].ProductionDate);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noPlan.StatusCode);
        Assert.Equal("DAY_HAS_NO_PLAN", (await noPlan.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task A_past_day_that_was_never_started_can_still_be_recorded_and_closed_late()
    {
        var client = await ClientAsync();
        // CR-01 OV-7 / N-09: ngày đã qua chưa Xuất hàng thì cho nhập bù và đóng muộn.
        var (order, days) = await CreateOrderFromAsync(client, Today.AddDays(-2), 100, 100, 100);

        (await PostEntryAsync(client, order.Id, days[0].ProductionDate, 70)).EnsureSuccessStatusCode();
        var closed = await CloseDayAsync(client, order.Id, days[0].ProductionDate);
        closed.EnsureSuccessStatusCode();

        // closed_at là thời điểm thực tế bấm nút, không backdate về ngày đó (CR-01 §14.2).
        var body = await closed.ReadAsync<CloseProductionDayResponse>();
        Assert.True(body.ClosedAt > DateTimeOffset.UtcNow.AddMinutes(-5));
    }
}
