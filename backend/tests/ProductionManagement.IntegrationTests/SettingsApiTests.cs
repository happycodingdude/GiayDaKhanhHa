using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace ProductionManagement.IntegrationTests;

/// <summary>Cấu hình vận hành — CR-01 §6.8, AC-21.</summary>
public class SettingsApiTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Bootstrap_creates_the_default_settings_row()
    {
        var client = await ClientAsync();

        var response = await client.GetAsync("/api/v1/settings");
        response.EnsureSuccessStatusCode();

        var settings = await response.ReadAsync<SystemSettingsResponse>();
        Assert.InRange(settings.RecordingIntervalMinutes, 5, 480);
        Assert.True(settings.RemindBeforeDue);
    }

    [Fact]
    public async Task The_settings_can_be_updated_and_read_back()
    {
        var client = await ClientAsync();

        var response = await client.PutAsJsonAsync(
            "/api/v1/settings", new { recordingIntervalMinutes = 90, remindBeforeDue = false });
        response.EnsureSuccessStatusCode();

        var settings = await (await client.GetAsync("/api/v1/settings")).ReadAsync<SystemSettingsResponse>();
        Assert.Equal(90, settings.RecordingIntervalMinutes);
        Assert.False(settings.RemindBeforeDue);

        // Trả lại mặc định: fixture của database dùng chung cho cả collection.
        (await client.PutAsJsonAsync(
            "/api/v1/settings", new { recordingIntervalMinutes = 60, remindBeforeDue = true }))
            .EnsureSuccessStatusCode();
    }

    [Theory]
    [InlineData(4)]
    [InlineData(600)]
    [InlineData(0)]
    public async Task An_interval_outside_the_allowed_range_is_rejected(int interval)
    {
        var client = await ClientAsync();

        var response = await client.PutAsJsonAsync(
            "/api/v1/settings", new { recordingIntervalMinutes = interval, remindBeforeDue = true });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", (await response.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task The_recording_interval_never_blocks_an_entry()
    {
        var client = await ClientAsync();
        // AC-21: chu kỳ chỉ để nhắc; server không dùng nó để từ chối request nào (CR-01 N-10).
        var (order, days) = await CreateOrderAsync(client, 100);

        (await PostEntryAsync(client, order.Id, days[0].ProductionDate, 10)).EnsureSuccessStatusCode();
        (await PostEntryAsync(client, order.Id, days[0].ProductionDate, 10)).EnsureSuccessStatusCode();
    }
}
