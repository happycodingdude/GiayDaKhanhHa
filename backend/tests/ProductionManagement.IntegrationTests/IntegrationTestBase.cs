using System.Net.Http.Json;
using Xunit;

namespace ProductionManagement.IntegrationTests;

[Collection(nameof(ApiCollection))]
public abstract class IntegrationTestBase(ApiFactory factory)
{
    protected ApiFactory Factory { get; } = factory;

    /// <summary>Ngày nghiệp vụ của API. Điều kiện hợp lệ của điều chỉnh phụ thuộc vào nó.</summary>
    protected DateOnly Today => Factory.Today;

    protected Task<HttpClient> ClientAsync() => Factory.CreateAuthenticatedClientAsync();

    private static int _sequence;

    protected static string NextShoeCode() => $"SH-{Interlocked.Increment(ref _sequence):D5}-{Guid.NewGuid():N}"[..20];

    private static string NextLineCode() => $"L{Guid.NewGuid():N}"[..12];

    // --- Dây chuyền -----------------------------------------------------------------------------

    /// <summary>
    /// Mỗi test dựng dây chuyền riêng với mã ngẫu nhiên. Các test chạy chung một database nên dùng
    /// lại một dây chuyền cố định sẽ khiến chúng nhìn thấy dữ liệu của nhau.
    /// </summary>
    protected static async Task<ProductionLineResponse> CreateLineAsync(
        HttpClient client, int sortOrder = 1, string? name = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/production-lines", new
        {
            code = NextLineCode(),
            name = name ?? $"Dây chuyền {sortOrder}",
            sortOrder,
            note = (string?)null,
        });

        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<ProductionLineResponse>();
    }

    // --- Nhập hàng ------------------------------------------------------------------------------

    /// <summary>
    /// Nhập hàng. Endpoint là multipart vì ảnh mẫu đi kèm ngay ở bước tạo (CR-001 §6.4); test không
    /// gửi ảnh thì chỉ có hai field text.
    /// </summary>
    protected static async Task<OrderResponse> ReceiveOrderAsync(
        HttpClient client, int quantity, string? shoeCode = null, (byte[] Bytes, string FileName, string ContentType)? image = null)
    {
        var response = await PostReceiptAsync(client, shoeCode ?? NextShoeCode(), quantity.ToString(), image);
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<OrderResponse>();
    }

    protected static Task<HttpResponseMessage> PostReceiptAsync(
        HttpClient client, string? shoeCode, string quantity,
        (byte[] Bytes, string FileName, string ContentType)? image = null)
        => client.PostAsync("/api/v1/orders", ReceiptForm(shoeCode, quantity, image));

    /// <summary>
    /// Sửa nhập hàng. Cũng là multipart: ảnh mới đi kèm, hoặc <c>removeImage</c> để gỡ ảnh — lưu
    /// cùng thông tin trong một lần.
    /// </summary>
    protected static Task<HttpResponseMessage> PutReceiptAsync(
        HttpClient client, Guid orderId, string shoeCode, int quantity,
        (byte[] Bytes, string FileName, string ContentType)? image = null, bool? removeImage = null)
    {
        var form = ReceiptForm(shoeCode, quantity.ToString(), image);

        if (removeImage is { } remove)
        {
            form.Add(new StringContent(remove ? "true" : "false"), "removeImage");
        }

        return client.PutAsync($"/api/v1/orders/{orderId}", form);
    }

    private static MultipartFormDataContent ReceiptForm(
        string? shoeCode, string quantity, (byte[] Bytes, string FileName, string ContentType)? image)
    {
        var form = new MultipartFormDataContent();

        if (shoeCode is not null)
        {
            form.Add(new StringContent(shoeCode), "shoeCode");
        }

        form.Add(new StringContent(quantity), "quantity");

        if (image is { } file)
        {
            var content = new ByteArrayContent(file.Bytes);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
            form.Add(content, "image", file.FileName);
        }

        return form;
    }

    // --- Lập tiến độ ----------------------------------------------------------------------------

    /// <summary>Một dây chuyền trong request lập tiến độ.</summary>
    protected sealed record LineAllocation(Guid ProductionLineId, params int[] DailyPlan)
    {
        public int Total => DailyPlan.Sum();
    }

    protected static Task<HttpResponseMessage> PostScheduleAsync(
        HttpClient client, Guid orderId, DateOnly startDate, DateOnly dueDate,
        IEnumerable<(Guid LineId, int Allocated, int[] DailyPlan)> lines, string allocationMode = "Manual")
        => client.PostAsJsonAsync($"/api/v1/orders/{orderId}/production-schedule", new
        {
            startDate = startDate.ToString("yyyy-MM-dd"),
            dueDate = dueDate.ToString("yyyy-MM-dd"),
            allocationMode,
            lines = lines.Select(l => new
            {
                productionLineId = l.LineId,
                allocatedQuantity = l.Allocated,
                plans = l.DailyPlan.Select((quantity, index) => new
                {
                    productionDate = startDate.AddDays(index).ToString("yyyy-MM-dd"),
                    plannedQuantity = quantity,
                }).ToArray(),
            }).ToArray(),
        });

    /// <summary>
    /// Đơn hàng đã lập tiến độ trên MỘT dây chuyền, bắt đầu từ hôm nay — cấu hình mặc định của phần
    /// lớn test. Chỉ ngày đầu tiên nhập được thực tế; các ngày sau nằm ở tương lai.
    /// </summary>
    protected Task<(OrderResponse Order, Guid LineId, IReadOnlyList<ProductionCellResponse> Cells)> CreateOrderAsync(
        HttpClient client, params int[] dailyPlan)
        => CreateOrderFromAsync(client, Today, dailyPlan);

    /// <summary>
    /// Như trên nhưng chọn được ngày bắt đầu. Dùng ngày bắt đầu trong quá khứ khi test cần ghi thực
    /// tế cho nhiều hơn một ngày: thực tế chỉ ghi được tới hôm nay.
    /// </summary>
    protected static async Task<(OrderResponse Order, Guid LineId, IReadOnlyList<ProductionCellResponse> Cells)>
        CreateOrderFromAsync(HttpClient client, DateOnly startDate, params int[] dailyPlan)
    {
        var line = await CreateLineAsync(client);
        var order = await ReceiveOrderAsync(client, dailyPlan.Sum());

        var response = await PostScheduleAsync(
            client, order.Id, startDate, startDate.AddDays(dailyPlan.Length - 1),
            [(line.Id, dailyPlan.Sum(), dailyPlan)]);

        response.EnsureSuccessStatusCode();

        return (await response.ReadAsync<OrderResponse>(), line.Id, await GetCellsAsync(client, order.Id));
    }

    // --- Đọc trạng thái -------------------------------------------------------------------------

    protected static async Task<IReadOnlyList<ProductionCellResponse>> GetCellsAsync(HttpClient client, Guid orderId)
        => (await GetMatrixAsync(client, orderId)).Items;

    protected static async Task<ProductionMatrixResponse> GetMatrixAsync(HttpClient client, Guid orderId)
    {
        var response = await client.GetAsync($"/api/v1/orders/{orderId}/production-plans");
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<ProductionMatrixResponse>();
    }

    protected static async Task<OrderResponse> GetOrderAsync(HttpClient client, Guid orderId)
    {
        var response = await client.GetAsync($"/api/v1/orders/{orderId}");
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<OrderResponse>();
    }

    // --- Sản lượng theo ô -----------------------------------------------------------------------

    private static string CellPath(Guid orderId, DateOnly date, Guid lineId)
        => $"/api/v1/orders/{orderId}/production-days/{date:yyyy-MM-dd}/lines/{lineId}";

    protected static Task<HttpResponseMessage> GetCellAsync(
        HttpClient client, Guid orderId, DateOnly date, Guid lineId)
        => client.GetAsync(CellPath(orderId, date, lineId));

    protected static Task<HttpResponseMessage> PostEntryAsync(
        HttpClient client, Guid orderId, DateOnly date, Guid lineId, int quantity, string? note = null)
        => client.PostAsJsonAsync($"{CellPath(orderId, date, lineId)}/entries", new { quantity, note });

    protected static Task<HttpResponseMessage> PutEntryAsync(
        HttpClient client, Guid entryId, int quantity, string? note = null)
        => client.PutAsJsonAsync($"/api/v1/production-entries/{entryId}", new { quantity, note });

    protected static Task<HttpResponseMessage> DeleteEntryAsync(HttpClient client, Guid entryId)
        => client.DeleteAsync($"/api/v1/production-entries/{entryId}");

    /// <summary>Xuất hàng chốt sổ cả ngày: mọi dây chuyền của ngày đó đóng cùng lúc.</summary>
    protected static Task<HttpResponseMessage> CloseDayAsync(HttpClient client, Guid orderId, DateOnly date)
        => client.PostAsync($"/api/v1/orders/{orderId}/production-days/{date:yyyy-MM-dd}/close", null);

    /// <summary>
    /// Ghi nhận rồi chốt sổ ngay — cách nhanh nhất để dựng một ô đã Xuất hàng, thứ mà mọi test về
    /// phần thiếu và điều chỉnh đều cần làm trước. Xuất hàng đóng CẢ NGÀY, nên với đơn nhiều dây
    /// chuyền, các dây chuyền khác của ngày đó cũng bị đóng theo.
    /// </summary>
    protected static async Task<ProductionCellDetailResponse> RecordAndCloseAsync(
        HttpClient client, Guid orderId, DateOnly date, Guid lineId, int quantity)
    {
        if (quantity > 0)
        {
            (await PostEntryAsync(client, orderId, date, lineId, quantity)).EnsureSuccessStatusCode();
        }

        (await CloseDayAsync(client, orderId, date)).EnsureSuccessStatusCode();

        return await (await GetCellAsync(client, orderId, date, lineId)).ReadAsync<ProductionCellDetailResponse>();
    }
}
