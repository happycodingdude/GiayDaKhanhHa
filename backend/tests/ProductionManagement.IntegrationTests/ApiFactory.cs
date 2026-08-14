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
/// Khởi động API thật trên một database PostgreSQL dùng một lần, để transaction, row lock và các
/// ràng buộc database đều được chạy thật.
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
    /// Database test dùng một lần. Để lộ ra để test dựng được trạng thái mà không endpoint nào tạo
    /// ra được — ví dụ đẩy một đơn hàng qua ngày hạn.
    /// </summary>
    public string TestConnectionString => ConnectionString;

    /// <summary>Ngày nghiệp vụ mà API nhìn thấy, để các luật phụ thuộc ngày là tất định.</summary>
    public DateOnly Today { get; private set; }

    public async Task InitializeAsync()
    {
        await using (var connection = new NpgsqlConnection(AdminConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", connection);
            await command.ExecuteNonQueryAsync();
        }

        // Việc dựng client sẽ kích hoạt migration + tạo tài khoản khởi tạo.
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

    /// <summary>Client đã đăng nhập. Cookie xác thực do cookie container của client mang theo.</summary>
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
