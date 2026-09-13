using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace ProductionManagement.IntegrationTests;

/// <summary>Danh mục dây chuyền sản xuất (CR-001 Slice 1).</summary>
public class ProductionLineApiTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    private static async Task<ProductionLineListResponse> ListAsync(HttpClient client, string? status = null)
    {
        var response = await client.GetAsync(
            status is null ? "/api/v1/production-lines" : $"/api/v1/production-lines?status={status}");

        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<ProductionLineListResponse>();
    }

    [Fact]
    public async Task A_new_line_is_active_and_not_in_use()
    {
        var client = await ClientAsync();

        var line = await CreateLineAsync(client);

        Assert.Equal("Active", line.Status);
        Assert.False(line.InUse);
        Assert.Contains((await ListAsync(client)).Items, l => l.Id == line.Id);
    }

    [Fact]
    public async Task A_duplicate_code_is_rejected_with_a_conflict()
    {
        var client = await ClientAsync();
        var line = await CreateLineAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/production-lines", new
        {
            code = line.Code,
            name = "Trùng mã",
            sortOrder = 9,
            note = (string?)null,
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("PRODUCTION_LINE_CODE_ALREADY_EXISTS", (await response.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task A_missing_code_or_name_is_a_validation_error()
    {
        var client = await ClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/production-lines", new
        {
            code = "   ",
            name = "",
            sortOrder = 1,
            note = (string?)null,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.ReadErrorAsync();
        Assert.Equal("VALIDATION_ERROR", error.Code);
        Assert.Contains(error.Details!, d => d.Field == "code");
        Assert.Contains(error.Details!, d => d.Field == "name");
    }

    [Fact]
    public async Task A_line_can_be_renamed()
    {
        var client = await ClientAsync();
        var line = await CreateLineAsync(client);

        var response = await client.PutAsJsonAsync($"/api/v1/production-lines/{line.Id}", new
        {
            code = line.Code,
            name = "Dây chuyền đã đổi tên",
            sortOrder = 5,
            note = "Ghi chú",
        });

        response.EnsureSuccessStatusCode();
        var updated = await response.ReadAsync<ProductionLineResponse>();

        Assert.Equal("Dây chuyền đã đổi tên", updated.Name);
        Assert.Equal(5, updated.SortOrder);
        Assert.Equal("Ghi chú", updated.Note);
    }

    [Fact]
    public async Task Deactivating_a_line_is_always_allowed_even_while_it_is_in_use()
    {
        var client = await ClientAsync();
        var (_, lineId, _) = await CreateOrderAsync(client, 100);

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/production-lines/{lineId}/status", new { status = "Inactive" });

        response.EnsureSuccessStatusCode();
        var updated = await response.ReadAsync<ProductionLineResponse>();

        // Tắt chỉ ảnh hưởng tới lựa chọn mới; đơn đang chạy trên nó không hề gì (BR-N14).
        Assert.Equal("Inactive", updated.Status);
        Assert.True(updated.InUse);
    }

    [Fact]
    public async Task A_line_used_by_an_order_is_reported_as_in_use()
    {
        var client = await ClientAsync();
        var (_, lineId, _) = await CreateOrderAsync(client, 100);

        var listed = Assert.Single((await ListAsync(client)).Items, l => l.Id == lineId);

        Assert.True(listed.InUse);
    }

    [Fact]
    public async Task The_list_can_be_filtered_by_status()
    {
        var client = await ClientAsync();
        var active = await CreateLineAsync(client);
        var inactive = await CreateLineAsync(client);

        (await client.PatchAsJsonAsync(
            $"/api/v1/production-lines/{inactive.Id}/status", new { status = "Inactive" })).EnsureSuccessStatusCode();

        var actives = await ListAsync(client, "Active");

        Assert.Contains(actives.Items, l => l.Id == active.Id);
        Assert.DoesNotContain(actives.Items, l => l.Id == inactive.Id);
    }

    [Fact]
    public async Task An_unknown_status_value_is_a_validation_error()
    {
        var client = await ClientAsync();
        var line = await CreateLineAsync(client);

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/production-lines/{line.Id}/status", new { status = "Paused" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Updating_a_missing_line_returns_a_not_found_error()
    {
        var client = await ClientAsync();

        var response = await client.PutAsJsonAsync($"/api/v1/production-lines/{Guid.NewGuid()}", new
        {
            code = "DC-X",
            name = "Không tồn tại",
            sortOrder = 1,
            note = (string?)null,
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("PRODUCTION_LINE_NOT_FOUND", (await response.ReadErrorAsync()).Code);
    }
}
