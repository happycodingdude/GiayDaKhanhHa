using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using ProductionManagement.Application.Abstractions;
using ProductionManagement.Domain.Entities;
using ProductionManagement.Infrastructure.Persistence;

namespace ProductionManagement.Api.Setup;

/// <summary>
/// Applies migrations and creates the first manager account.
/// No password is ever hard-coded in a migration (Step 3 §15): the password comes from
/// configuration, and when none is configured a random one is generated and logged once.
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

        if (await db.Users.AnyAsync())
        {
            return;
        }

        var username = configuration["Bootstrap:Username"] ?? "manager";
        var displayName = configuration["Bootstrap:DisplayName"] ?? "Quản lý sản xuất";

        var configuredPassword = configuration["Bootstrap:Password"];
        var password = string.IsNullOrWhiteSpace(configuredPassword)
            ? GeneratePassword()
            : configuredPassword;

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        db.Users.Add(User.Create(username, hasher.Hash(password), displayName, clock.UtcNow));
        await db.SaveChangesAsync();

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

    private static string GeneratePassword()
    {
        const string alphabet = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return RandomNumberGenerator.GetString(alphabet, 16);
    }
}
