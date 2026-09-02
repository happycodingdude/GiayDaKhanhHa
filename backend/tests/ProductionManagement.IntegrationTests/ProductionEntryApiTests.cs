using System.Net;
using Npgsql;
using Xunit;

namespace ProductionManagement.IntegrationTests;

/// <summary>
/// Ghi nhận sản lượng nhiều lần trong ngày — CR-01 AC-01..AC-06, AC-17, AC-18, AC-20.
/// </summary>
public class ProductionEntryApiTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    private static async Task<ProductionDayDetailResponse> DayAsync(
        HttpClient client, Guid orderId, DateOnly date)
        => await (await GetDayAsync(client, orderId, date)).ReadAsync<ProductionDayDetailResponse>();

    [Fact]
    public async Task Several_entries_accumulate_into_the_day_total()
    {
        var client = await ClientAsync();
        var (order, days) = await CreateOrderAsync(client, 200, 200);

        (await PostEntryAsync(client, order.Id, days[0].ProductionDate, 15, "Tổ 2 vào ca")).EnsureSuccessStatusCode();
        (await PostEntryAsync(client, order.Id, days[0].ProductionDate, 20)).EnsureSuccessStatusCode();
        var third = await PostEntryAsync(client, order.Id, days[0].ProductionDate, 20);
        third.EnsureSuccessStatusCode();

        // POST trả luôn state đầy đủ của ngày, nên frontend không phải refetch (CR-01 §7.4).
        var day = await third.ReadAsync<ProductionDayDetailResponse>();
        Assert.Equal(55, day.DayActualQuantity);
        Assert.Equal(145, day.RemainingAllowance);
        Assert.Equal("DailyPlan", day.RemainingAllowanceReason);
        Assert.Equal(3, day.Entries.Count);

        // Mới nhất trên cùng, nhưng tổng lũy kế tính theo thứ tự thời gian (CR-01 §6.3, §8.1).
        Assert.Equal([55, 35, 15], day.Entries.Select(e => e.RunningTotal).ToArray());
        Assert.Equal("Tổ 2 vào ca", day.Entries[^1].Note);
    }

    [Fact]
    public async Task An_entry_beyond_the_daily_plan_is_rejected_and_names_the_allowance()
    {
        var client = await ClientAsync();
        var (order, days) = await CreateOrderAsync(client, 120, 120);

        (await PostEntryAsync(client, order.Id, days[0].ProductionDate, 90)).EnsureSuccessStatusCode();

        // AC-01: kế hoạch 120, đã nhập 90, ghi thêm 40.
        var response = await PostEntryAsync(client, order.Id, days[0].ProductionDate, 40);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var error = await response.ReadErrorAsync();
        Assert.Equal("ENTRY_EXCEEDS_DAILY_PLAN", error.Code);
        Assert.NotNull(error.Details);
        Assert.Equal("MAX_ALLOWED", error.Details.Single().Code);
        Assert.Equal("30", error.Details.Single().Message);

        // AC-02: đúng phần còn được nhập thì thành công.
        var accepted = await PostEntryAsync(client, order.Id, days[0].ProductionDate, 30);
        accepted.EnsureSuccessStatusCode();

        var day = await accepted.ReadAsync<ProductionDayDetailResponse>();
        Assert.Equal(120, day.DayActualQuantity);
        Assert.Equal(0, day.RemainingAllowance);
    }

    [Fact]
    public async Task The_order_quantity_caps_the_allowance_when_it_is_the_tighter_bound()
    {
        var client = await ClientAsync();
        // AC-03. Với dữ liệu sinh ra sau CR-01, trần ngày luôn là ràng buộc chặt hơn hoặc bằng: mỗi
        // ngày bị chặn ở kế hoạch của nó, và khoản bù luôn đúng bằng phần thiếu của ngày khác.
        // Trần đơn hàng chỉ trở thành ràng buộc chặt hơn với dữ liệu có TRƯỚC CR-01, khi một ngày
        // được phép vượt kế hoạch (OV-3) — đúng loại dòng mà migration mang sang. Nên trạng thái đó
        // được dựng thẳng trong database, giống cách OverdueOrderTests làm đơn hàng "già" đi.
        var (order, days) = await CreateOrderFromAsync(client, Today.AddDays(-1), 100, 120);

        await RecordAndCloseAsync(client, order.Id, days[0].ProductionDate, 100);
        await OverProduceLegacyDayAsync(order.Id, days[0].ProductionDate, 110);

        (await PostEntryAsync(client, order.Id, days[1].ProductionDate, 90)).EnsureSuccessStatusCode();

        // Trần ngày còn 30, nhưng đơn hàng 220 chỉ còn 220 - 200 = 20.
        var day = await DayAsync(client, order.Id, days[1].ProductionDate);
        Assert.Equal(20, day.RemainingAllowance);
        Assert.Equal("OrderQuantity", day.RemainingAllowanceReason);

        var response = await PostEntryAsync(client, order.Id, days[1].ProductionDate, 30);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var error = await response.ReadErrorAsync();
        Assert.Equal("ACTUAL_EXCEEDS_ORDER_QUANTITY", error.Code);
        Assert.Equal("20", error.Details!.Single().Message);

        (await PostEntryAsync(client, order.Id, days[1].ProductionDate, 20)).EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Nâng sản lượng của một ngày đã đóng lên trên kế hoạch của nó — trạng thái hợp lệ trước CR-01
    /// và không endpoint nào sau CR-01 tạo ra được nữa.
    /// </summary>
    private async Task OverProduceLegacyDayAsync(Guid orderId, DateOnly date, int actualQuantity)
    {
        await using var connection = new NpgsqlConnection(Factory.TestConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            WITH day AS (
                UPDATE production_days SET actual_quantity = @actual
                WHERE order_id = @orderId AND production_date = @date
                RETURNING id)
            UPDATE production_entries e SET quantity = @actual
            FROM day WHERE e.production_day_id = day.id AND e.deleted_at IS NULL
            """, connection);
        command.Parameters.AddWithValue("actual", actualQuantity);
        command.Parameters.AddWithValue("orderId", orderId);
        command.Parameters.AddWithValue("date", date);

        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    [Fact]
    public async Task A_day_planned_for_zero_cannot_receive_an_entry()
    {
        var client = await ClientAsync();
        // AC-04.
        var (order, days) = await CreateOrderFromAsync(client, Today.AddDays(-1), 0, 100);

        var response = await PostEntryAsync(client, order.Id, days[0].ProductionDate, 10);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("DAY_HAS_NO_PLAN", (await response.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task A_date_outside_the_production_plan_cannot_receive_an_entry()
    {
        var client = await ClientAsync();
        var (order, _) = await CreateOrderAsync(client, 100);

        var response = await PostEntryAsync(client, order.Id, Today.AddDays(-30), 10);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("DAY_HAS_NO_PLAN", (await response.ReadErrorAsync()).Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task An_entry_of_zero_or_less_is_rejected(int quantity)
    {
        var client = await ClientAsync();
        // AC-05: ghi nhận bằng 0 là vô nghĩa — "cả ngày không sản xuất được" thể hiện bằng
        // Xuất hàng với 0 lần ghi nhận.
        var (order, days) = await CreateOrderAsync(client, 100);

        var response = await PostEntryAsync(client, order.Id, days[0].ProductionDate, quantity);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", (await response.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task An_open_day_has_no_shortage_and_no_difference()
    {
        var client = await ClientAsync();
        // AC-06 — test quan trọng nhất của CR: null, KHÔNG phải 0.
        var (order, days) = await CreateOrderAsync(client, 200, 200);

        (await PostEntryAsync(client, order.Id, days[0].ProductionDate, 60)).EnsureSuccessStatusCode();

        var day = await DayAsync(client, order.Id, days[0].ProductionDate);
        Assert.Null(day.ShortageQuantity);
        Assert.Null(day.Difference);
        Assert.True(day.IsProvisional);
        Assert.Equal("InProduction", day.DayStatus);

        var timeline = await GetDaysAsync(client, order.Id);
        Assert.Null(timeline[0].ShortageQuantity);
        Assert.Null(timeline[0].Difference);
        Assert.Equal(60, timeline[0].ActualQuantity);
        Assert.True(timeline[0].IsProvisional);
    }

    [Fact]
    public async Task A_future_day_cannot_receive_an_entry()
    {
        var client = await ClientAsync();
        // AC-20. Ngày 0 là hôm nay, ngày 1 là ngày mai.
        var (order, days) = await CreateOrderAsync(client, 100, 100);

        var response = await PostEntryAsync(client, order.Id, days[1].ProductionDate, 40);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("FUTURE_DATE_NOT_ALLOWED", (await response.ReadErrorAsync()).Code);

        var timeline = await GetDaysAsync(client, order.Id);
        Assert.Null(timeline[1].ActualQuantity);
        Assert.Equal("NotStarted", timeline[1].DayStatus);
    }

    [Fact]
    public async Task Updating_an_entry_revalidates_with_the_replacement_formula()
    {
        var client = await ClientAsync();
        // AC-17.
        var (order, days) = await CreateOrderAsync(client, 100, 100);

        var created = await PostEntryAsync(client, order.Id, days[0].ProductionDate, 25);
        var entryId = (await created.ReadAsync<ProductionDayDetailResponse>()).Entries[0].Id;
        (await PostEntryAsync(client, order.Id, days[0].ProductionDate, 60)).EnsureSuccessStatusCode();

        // Đã nhập 85/100. Nâng 25 lên 45 sẽ thành 105 — vượt trần ngày.
        var tooHigh = await PutEntryAsync(client, entryId, 45);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooHigh.StatusCode);
        Assert.Equal("ENTRY_EXCEEDS_DAILY_PLAN", (await tooHigh.ReadErrorAsync()).Code);

        // NewDayActual = 85 - 25 + 10 = 70.
        var updated = await PutEntryAsync(client, entryId, 10);
        updated.EnsureSuccessStatusCode();

        var day = await updated.ReadAsync<ProductionDayDetailResponse>();
        Assert.Equal(70, day.DayActualQuantity);
        Assert.True(day.Entries.Single(e => e.Id == entryId).IsEdited);
    }

    [Fact]
    public async Task Deleting_an_entry_removes_it_from_every_total()
    {
        var client = await ClientAsync();
        // AC-18: xoá mềm — entry biến khỏi entries[] và khỏi mọi phép SUM.
        var (order, days) = await CreateOrderAsync(client, 100, 100);

        var created = await PostEntryAsync(client, order.Id, days[0].ProductionDate, 30);
        var entryId = (await created.ReadAsync<ProductionDayDetailResponse>()).Entries[0].Id;
        (await PostEntryAsync(client, order.Id, days[0].ProductionDate, 20)).EnsureSuccessStatusCode();

        var deleted = await DeleteEntryAsync(client, entryId);
        deleted.EnsureSuccessStatusCode();

        var day = await deleted.ReadAsync<ProductionDayDetailResponse>();
        Assert.Equal(20, day.DayActualQuantity);
        Assert.Single(day.Entries);
        Assert.DoesNotContain(day.Entries, e => e.Id == entryId);
        Assert.Equal(20, (await GetOrderAsync(client, order.Id)).TotalActual);
    }

    [Fact]
    public async Task An_unknown_entry_cannot_be_updated_or_deleted()
    {
        var client = await ClientAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await PutEntryAsync(client, Guid.NewGuid(), 10)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await DeleteEntryAsync(client, Guid.NewGuid())).StatusCode);
    }

    [Fact]
    public async Task The_order_total_includes_the_provisional_quantity_of_open_days()
    {
        var client = await ClientAsync();
        // Cố ý: nếu không, quản lý có thể nhập vượt tổng đơn trong ngày cuối (CR-01 §4.5).
        var (order, days) = await CreateOrderFromAsync(client, Today.AddDays(-1), 100, 100);

        await RecordAndCloseAsync(client, order.Id, days[0].ProductionDate, 100);
        (await PostEntryAsync(client, order.Id, days[1].ProductionDate, 40)).EnsureSuccessStatusCode();

        Assert.Equal(140, (await GetOrderAsync(client, order.Id)).TotalActual);
    }
}
