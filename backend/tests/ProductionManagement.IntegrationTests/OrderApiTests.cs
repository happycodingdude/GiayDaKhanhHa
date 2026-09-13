using System.Net;
using Xunit;

namespace ProductionManagement.IntegrationTests;

/// <summary>Nhập hàng (CR-001 Slice 2): mã giày, số lượng, tối đa một ảnh mẫu.</summary>
public class OrderApiTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task A_received_order_is_pending_with_no_schedule_yet()
    {
        var client = await ClientAsync();

        var order = await ReceiveOrderAsync(client, 1000);

        Assert.Equal("Pending", order.Status);
        Assert.Equal(1000, order.Quantity);
        Assert.Null(order.StartDate);
        Assert.Null(order.DueDate);
        Assert.Empty(order.ProductionLines);
        Assert.False(order.HasImage);
        Assert.Equal(1000, order.Remaining);
        Assert.Equal(0, order.TotalActual);
    }

    [Fact]
    public async Task A_duplicate_shoe_code_is_rejected_with_a_conflict()
    {
        var client = await ClientAsync();
        var shoeCode = NextShoeCode();

        (await PostReceiptAsync(client, shoeCode, "10")).EnsureSuccessStatusCode();

        var second = await PostReceiptAsync(client, shoeCode, "10");

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("SHOE_CODE_ALREADY_EXISTS", (await second.ReadErrorAsync()).Code);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-10")]
    public async Task A_non_positive_quantity_is_rejected(string quantity)
    {
        var client = await ClientAsync();

        var response = await PostReceiptAsync(client, NextShoeCode(), quantity);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_fractional_quantity_never_reaches_the_domain()
    {
        var client = await ClientAsync();

        // Model binding từ chối "100.5" cho một int trước cả khi vào service.
        var response = await PostReceiptAsync(client, NextShoeCode(), "100.5");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_missing_shoe_code_is_a_validation_error_and_nothing_is_stored()
    {
        var client = await ClientAsync();

        var response = await PostReceiptAsync(client, "   ", "10");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", (await response.ReadErrorAsync()).Code);
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
    public async Task The_shoe_code_can_be_corrected_while_the_order_is_pending()
    {
        var client = await ClientAsync();
        var order = await ReceiveOrderAsync(client, 100);
        var newCode = NextShoeCode();

        var response = await PutReceiptAsync(client, order.Id, newCode, 200);

        response.EnsureSuccessStatusCode();
        var updated = await response.ReadAsync<OrderResponse>();

        Assert.Equal(newCode, updated.ShoeCode);
        Assert.Equal(200, updated.Quantity);
    }

    [Fact]
    public async Task The_quantity_is_frozen_once_the_order_has_a_schedule()
    {
        var client = await ClientAsync();
        var (order, _, _) = await CreateOrderAsync(client, 100);

        var response = await PutReceiptAsync(client, order.Id, order.ShoeCode, 90);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("ORDER_SCHEDULE_ALREADY_EXISTS", (await response.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task A_pending_order_is_deleted_together_with_its_image_file()
    {
        var client = await ClientAsync();
        var jpeg = new byte[512];
        jpeg[0] = 0xFF;
        jpeg[1] = 0xD8;
        jpeg[2] = 0xFF;
        var order = await ReceiveOrderAsync(client, 100, image: (jpeg, "mau.jpg", "image/jpeg"));

        var response = await client.DeleteAsync($"/api/v1/orders/{order.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var after = await client.GetAsync($"/api/v1/orders/{order.Id}");
        Assert.Equal(HttpStatusCode.NotFound, after.StatusCode);
        Assert.Empty(Factory.ImageFilesOf(order.Id));
    }

    [Fact]
    public async Task The_shoe_code_of_a_deleted_order_can_be_used_again()
    {
        var client = await ClientAsync();
        var order = await ReceiveOrderAsync(client, 100);

        (await client.DeleteAsync($"/api/v1/orders/{order.Id}")).EnsureSuccessStatusCode();

        // Xoá cứng: mã giày được giải phóng, nhập lại lô hàng đúng mã đó không bị báo trùng.
        var again = await PostReceiptAsync(client, order.ShoeCode, "100");
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }

    [Fact]
    public async Task A_scheduled_order_cannot_be_deleted()
    {
        var client = await ClientAsync();
        var (order, _, _) = await CreateOrderAsync(client, 100);

        var response = await client.DeleteAsync($"/api/v1/orders/{order.Id}");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("ORDER_NOT_DELETABLE", (await response.ReadErrorAsync()).Code);

        (await client.GetAsync($"/api/v1/orders/{order.Id}")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Deleting_a_missing_order_returns_a_not_found_error()
    {
        var client = await ClientAsync();

        var response = await client.DeleteAsync($"/api/v1/orders/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("ORDER_NOT_FOUND", (await response.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task The_order_list_can_be_filtered_by_status()
    {
        var client = await ClientAsync();
        var (order, lineId, cells) = await CreateOrderAsync(client, 10);

        // Trạng thái đơn hàng chỉ được đánh giá tại thời điểm Xuất hàng (CR-01 OV-4).
        await RecordAndCloseAsync(client, order.Id, cells[0].ProductionDate, lineId, 10);

        var completed = await client.GetAsync("/api/v1/orders?status=Completed&pageSize=200");
        completed.EnsureSuccessStatusCode();
        Assert.Contains(order.ShoeCode, await completed.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var incomplete = await client.GetAsync("/api/v1/orders?status=Incomplete&pageSize=200");
        incomplete.EnsureSuccessStatusCode();
        Assert.DoesNotContain(order.ShoeCode, await incomplete.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pending_orders_are_excluded_from_the_scheduled_filter()
    {
        var client = await ClientAsync();
        var pending = await ReceiveOrderAsync(client, 100);
        var (scheduled, _, _) = await CreateOrderAsync(client, 100);

        var response = await client.GetAsync("/api/v1/orders?status=Scheduled&pageSize=200");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains(scheduled.ShoeCode, body, StringComparison.Ordinal);
        Assert.DoesNotContain(pending.ShoeCode, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_order_list_can_be_filtered_by_production_line()
    {
        var client = await ClientAsync();
        var (mine, lineId, _) = await CreateOrderAsync(client, 100);
        var (other, _, _) = await CreateOrderAsync(client, 100);

        var response = await client.GetAsync($"/api/v1/orders?productionLineId={lineId}&pageSize=200");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains(mine.ShoeCode, body, StringComparison.Ordinal);
        Assert.DoesNotContain(other.ShoeCode, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Searching_matches_the_shoe_code()
    {
        var client = await ClientAsync();
        var order = await ReceiveOrderAsync(client, 100);

        var response = await client.GetAsync($"/api/v1/orders?search={order.ShoeCode.ToLowerInvariant()}");
        response.EnsureSuccessStatusCode();

        Assert.Contains(order.ShoeCode, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }
}
