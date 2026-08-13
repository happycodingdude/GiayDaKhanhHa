using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ProductionManagement.Application.Abstractions;
using Xunit;

namespace ProductionManagement.IntegrationTests;

/// <summary>
/// Boots the real API against a throwaway PostgreSQL database so transactions, row locking and the
/// database constraints are all exercised for real.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>
    /// Source không chứa bất kỳ thông tin cấu hình nào: địa chỉ server đến từ
    /// <c>PM_TEST_POSTGRES</c>, mật khẩu do Npgsql đọc từ <c>PGPASSWORD</c>. Xem README mục 2.1.
    /// </summary>
    private static readonly string ServerConnectionString =
        Environment.GetEnvironmentVariable("PM_TEST_POSTGRES")
        ?? throw new InvalidOperationException(
            "PM_TEST_POSTGRES chưa được đặt. Nạp biến môi trường từ .env trước khi chạy test "
            + "(ví dụ: Host=localhost;Port=5432;Username=postgres).");

    private static string AdminConnectionString => $"{ServerConnectionString};Database=postgres";

    public static readonly string TestPassword =
        Environment.GetEnvironmentVariable("PM_TEST_PASSWORD") ?? Guid.NewGuid().ToString("N");

    public const string TestUsername = "tester";

    private readonly string _databaseName = $"pm_test_{Guid.NewGuid():N}";

    private string ConnectionString => $"{ServerConnectionString};Database={_databaseName}";

    /// <summary>
    /// The throwaway test database. Exposed so a test can set up state that no endpoint can
    /// produce — ageing an order past its due date, for instance.
    /// </summary>
    public string TestConnectionString => ConnectionString;

    /// <summary>The business date the API sees, so date-dependent rules are deterministic.</summary>
    public DateOnly Today { get; private set; }

    public async Task InitializeAsync()
    {
        await using (var connection = new NpgsqlConnection(AdminConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", connection);
            await command.ExecuteNonQueryAsync();
        }

        // Building the client triggers migration + bootstrap-user creation.
        using var _ = CreateClient();
        Today = Services.GetRequiredService<IClock>().Today;
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        NpgsqlConnection.ClearAllPools();

        await using var connection = new NpgsqlConnection(AdminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);
        builder.UseSetting("Bootstrap:Username", TestUsername);
        builder.UseSetting("Bootstrap:Password", TestPassword);
        builder.UseSetting("Bootstrap:DisplayName", "Tester");
        builder.UseSetting("Database:AutoMigrate", "true");
        builder.UseSetting("Business:TimeZone", "UTC");
    }

    /// <summary>An authenticated client. The auth cookie is carried by the client's cookie container.</summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { username = TestUsername, password = TestPassword });

        response.EnsureSuccessStatusCode();
        return client;
    }

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}

[CollectionDefinition(nameof(ApiCollection))]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>;
