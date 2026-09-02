using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using ProductionManagement.Application.Abstractions;
using ProductionManagement.Application.Features.Settings;
using ProductionManagement.Domain.Entities;
using ProductionManagement.Infrastructure.Persistence;

namespace ProductionManagement.Api.Setup;

/// <summary>
/// Chạy migration và tạo tài khoản quản lý đầu tiên.
/// Không bao giờ hard-code mật khẩu trong migration (Step 3 §15): mật khẩu lấy từ cấu hình, và khi
/// không cấu hình gì thì sinh ngẫu nhiên rồi ghi log đúng một lần.
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (configuration.GetValue("Database:AutoMigrate", true))
        {
            await db.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied.");
        }

        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        if (await db.Users.AnyAsync())
        {
            await EnsureSystemSettingsAsync(db, clock, logger);
            return;
        }

        var username = configuration["Bootstrap:Username"] ?? "manager";
        var displayName = configuration["Bootstrap:DisplayName"] ?? "Quản lý sản xuất";

        var configuredPassword = configuration["Bootstrap:Password"];
        var password = string.IsNullOrWhiteSpace(configuredPassword)
            ? GeneratePassword()
            : configuredPassword;

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var user = User.Create(username, hasher.Hash(password), displayName, clock.UtcNow);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        await EnsureSystemSettingsAsync(db, clock, logger);

        if (string.IsNullOrWhiteSpace(configuredPassword))
        {
            logger.LogWarning(
                "Created the initial account '{Username}' with generated password: {Password}. " +
                "Store it now — it is not shown again. Set Bootstrap__Password to choose it yourself.",
                username, password);
        }
        else
        {
            logger.LogInformation("Created the initial account '{Username}' from configuration.", username);
        }
    }

    /// <summary>
    /// Cấu hình vận hành có đúng một dòng, tạo bởi bootstrap chứ không hard-code trong migration
    /// (CR-01 §5.4). Giá trị mặc định: chu kỳ 60 phút, có nhắc trước khi tới hạn.
    /// </summary>
    private static async Task EnsureSystemSettingsAsync(AppDbContext db, IClock clock, ILogger logger)
    {
        if (await db.SystemSettings.AnyAsync())
        {
            return;
        }

        // updated_by trỏ về tài khoản đầu tiên: cột này NOT NULL và mọi dòng đều phải truy được
        // người chịu trách nhiệm.
        var firstUserId = await db.Users.OrderBy(u => u.CreatedAt).Select(u => u.Id).FirstAsync();

        db.SystemSettings.Add(SystemSettings.Create(
            SettingsService.DefaultRecordingIntervalMinutes,
            SettingsService.DefaultRemindBeforeDue,
            firstUserId,
            clock.UtcNow));

        await db.SaveChangesAsync();
        logger.LogInformation("Created the default system settings row.");
    }

    private static string GeneratePassword()
    {
        const string alphabet = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return RandomNumberGenerator.GetString(alphabet, 16);
    }
}
