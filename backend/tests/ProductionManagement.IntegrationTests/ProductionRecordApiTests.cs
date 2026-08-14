using System.Net;
using Xunit;

namespace ProductionManagement.IntegrationTests;

public class ProductionRecordApiTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Recording_an_actual_produces_the_derived_shortage_and_difference()
    {
        var client = await ClientAsync();
        var (order, days) = await CreateOrderAsync(client, 100, 100);

        (await PostActualAsync(client, order.Id, days[0].ProductionDate, 80)).EnsureSuccessStatusCode();

        var updated = await GetDaysAsync(client, order.Id);
        Assert.Equal(80, updated[0].ActualQuantity);
        Assert.Equal(20, updated[0].ShortageQuantity);
        Assert.Equal(-20, updated[0].Difference);
        // Ngày không đụng tới vẫn hoàn toàn chưa có bản ghi nào.
        Assert.Null(updated[1].ActualQuantity);
        Assert.Equal(0, updated[1].ShortageQuantity);
    }

    [Fact]
    public async Task An_explicit_zero_actual_is_recorded_and_is_distinct_from_no_record()
    {
        var client = await ClientAsync();
        var (order, days) = await CreateOrderAsync(client, 100, 100);

        (await PostActualAsync(client, order.Id, days[0].ProductionDate, 0)).EnsureSuccessStatusCode();

        var updated = await GetDaysAsync(client, order.Id);
        Assert.Equal(0, updated[0].ActualQuantity);
        Assert.NotNull(updated[0].ProductionRecordId);
        Assert.Equal(100, updated[0].ShortageQuantity);

        Assert.Null(updated[1].ActualQuantity);
        Assert.Null(updated[1].ProductionRecordId);
    }

    [Fact]
    public async Task A_second_record_for_the_same_day_is_rejected()
    {
        var client = await ClientAsync();
        var (order, days) = await CreateOrderAsync(client, 100);

        (await PostActualAsync(client, order.Id, days[0].ProductionDate, 40)).EnsureSuccessStatusCode();
        var second = await PostActualAsync(client, order.Id, days[0].ProductionDate, 30);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("PRODUCTION_RECORD_ALREADY_EXISTS", (await second.ReadErrorAsync()).Code);

        // Giá trị đầu tiên không bị đụng tới: không có chuyện cộng dồn.
        var updated = await GetDaysAsync(client, order.Id);
        Assert.Equal(40, updated[0].ActualQuantity);
    }

    [Fact]
    public async Task Editing_replaces_the_recorded_value()
    {
        var client = await ClientAsync();
        var (order, days) = await CreateOrderAsync(client, 100, 100);

        var created = await PostActualAsync(client, order.Id, days[0].ProductionDate, 80);
        var record = await created.ReadAsync<ProductionRecordResponse>();

        (await PutActualAsync(client, order.Id, record.Id, 75)).EnsureSuccessStatusCode();

        var updated = await GetDaysAsync(client, order.Id);
        Assert.Equal(75, updated[0].ActualQuantity);
        Assert.Equal(75, (await GetOrderAsync(client, order.Id)).TotalActual);
    }

    [Fact]
    public async Task The_total_actual_can_never_exceed_the_order_quantity()
    {
        var client = await ClientAsync();
        // Đơn hàng 500 đơn vị trải trên hai ngày, ngày thứ hai là hôm nay.
        var (order, days) = await CreateOrderFromAsync(client, Today.AddDays(-1), 250, 250);

        (await PostActualAsync(client, order.Id, days[0].ProductionDate, 450)).EnsureSuccessStatusCode();

        // 450 + 60 sẽ thành 510 trên một đơn hàng 500 đơn vị.
        var response = await PostActualAsync(client, order.Id, days[1].ProductionDate, 60);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("ACTUAL_EXCEEDS_ORDER_QUANTITY", (await response.ReadErrorAsync()).Code);

        // Đúng bằng phần còn lại thì được chấp nhận.
        (await PostActualAsync(client, order.Id, days[1].ProductionDate, 50)).EnsureSuccessStatusCode();
        Assert.Equal(500, (await GetOrderAsync(client, order.Id)).TotalActual);
    }

    [Fact]
    public async Task Editing_is_validated_against_the_total_excluding_the_edited_day()
    {
        var client = await ClientAsync();
        var (order, days) = await CreateOrderFromAsync(client, Today.AddDays(-1), 500, 500);

        var first = await PostActualAsync(client, order.Id, days[0].ProductionDate, 300);
        var firstRecord = await first.ReadAsync<ProductionRecordResponse>();
        (await PostActualAsync(client, order.Id, days[1].ProductionDate, 600)).EnsureSuccessStatusCode();

        // Đã ghi 900, còn dư địa 100: nâng ngày 1 từ 300 lên 450 sẽ thành tổng 1050.
        var tooHigh = await PutActualAsync(client, order.Id, firstRecord.Id, 450);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooHigh.StatusCode);

        // NewTotal = CurrentTotal - OldActual + NewActual, nên 400 là hợp lệ.
        (await PutActualAsync(client, order.Id, firstRecord.Id, 400)).EnsureSuccessStatusCode();
        Assert.Equal(1000, (await GetOrderAsync(client, order.Id)).TotalActual);
    }

    [Fact]
    public async Task A_day_planned_for_zero_cannot_receive_an_actual_not_even_zero()
    {
        var client = await ClientAsync();
        var (order, days) = await CreateOrderAsync(client, 100, 0);

        var response = await PostActualAsync(client, order.Id, days[1].ProductionDate, 0);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("PLAN_QUANTITY_IS_ZERO", (await response.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task A_production_day_that_has_not_arrived_yet_cannot_receive_an_actual()
    {
        var client = await ClientAsync();
        // Ngày 0 là hôm nay, ngày 1 là ngày mai.
        var (order, days) = await CreateOrderAsync(client, 100, 100);

        var response = await PostActualAsync(client, order.Id, days[1].ProductionDate, 40);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("FUTURE_PRODUCTION_DATE", (await response.ReadErrorAsync()).Code);

        // Không có gì được ghi.
        Assert.Null((await GetDaysAsync(client, order.Id))[1].ActualQuantity);
    }

    [Fact]
    public async Task Today_and_past_days_can_receive_an_actual()
    {
        var client = await ClientAsync();
        // Ngày 0 là hôm qua, ngày 1 là hôm nay — cả hai đều nằm trong khoảng cho phép.
        var (order, days) = await CreateOrderFromAsync(client, Today.AddDays(-1), 100, 100);

        (await PostActualAsync(client, order.Id, days[0].ProductionDate, 90)).EnsureSuccessStatusCode();
        (await PostActualAsync(client, order.Id, days[1].ProductionDate, 80)).EnsureSuccessStatusCode();

        Assert.Equal(170, (await GetOrderAsync(client, order.Id)).TotalActual);
    }

    [Fact]
    public async Task A_date_without_a_production_plan_cannot_receive_an_actual()
    {
        var client = await ClientAsync();
        var (order, _) = await CreateOrderAsync(client, 100);

        var response = await PostActualAsync(client, order.Id, Today.AddDays(30), 10);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("NO_PRODUCTION_PLAN_FOR_DATE", (await response.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task A_negative_actual_is_rejected()
    {
        var client = await ClientAsync();
        var (order, days) = await CreateOrderAsync(client, 100);

        var response = await PostActualAsync(client, order.Id, days[0].ProductionDate, -5);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", (await response.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task An_actual_above_the_daily_plan_is_allowed_while_the_order_total_still_fits()
    {
        var client = await ClientAsync();
        var (order, days) = await CreateOrderAsync(client, 100, 100);

        (await PostActualAsync(client, order.Id, days[0].ProductionDate, 130)).EnsureSuccessStatusCode();

        var updated = await GetDaysAsync(client, order.Id);
        Assert.Equal(30, updated[0].Difference);
        Assert.Equal(0, updated[0].ShortageQuantity);
    }

    [Fact]
    public async Task The_order_completes_when_the_total_actual_reaches_the_quantity()
    {
        var client = await ClientAsync();
        var (order, days) = await CreateOrderFromAsync(client, Today.AddDays(-1), 60, 40);

        (await PostActualAsync(client, order.Id, days[0].ProductionDate, 60)).EnsureSuccessStatusCode();
        Assert.Equal("Incomplete", (await GetOrderAsync(client, order.Id)).Status);

        (await PostActualAsync(client, order.Id, days[1].ProductionDate, 40)).EnsureSuccessStatusCode();

        var completed = await GetOrderAsync(client, order.Id);
        Assert.Equal("Completed", completed.Status);
        Assert.Equal(0, completed.Remaining);
        Assert.Equal(100m, completed.ProgressPercentage);
    }

    [Fact]
    public async Task Correcting_an_actual_downwards_reopens_a_completed_order()
    {
        var client = await ClientAsync();
        var (order, days) = await CreateOrderAsync(client, 100);

        var created = await PostActualAsync(client, order.Id, days[0].ProductionDate, 100);
        var record = await created.ReadAsync<ProductionRecordResponse>();
        Assert.Equal("Completed", (await GetOrderAsync(client, order.Id)).Status);

        (await PutActualAsync(client, order.Id, record.Id, 80)).EnsureSuccessStatusCode();

        var reopened = await GetOrderAsync(client, order.Id);
        Assert.Equal("Incomplete", reopened.Status);
        Assert.Equal(20, reopened.Remaining);
    }

    [Fact]
    public async Task Editing_a_record_that_belongs_to_a_different_order_is_not_found()
    {
        var client = await ClientAsync();
        var (orderA, daysA) = await CreateOrderAsync(client, 100);
        var (orderB, _) = await CreateOrderAsync(client, 100);

        var created = await PostActualAsync(client, orderA.Id, daysA[0].ProductionDate, 50);
        var record = await created.ReadAsync<ProductionRecordResponse>();

        var response = await PutActualAsync(client, orderB.Id, record.Id, 10);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
