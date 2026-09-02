using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProductionManagement.Application.Abstractions;
using ProductionManagement.Application.Features.Adjustments;
using ProductionManagement.Application.Features.Auth;
using ProductionManagement.Application.Features.Orders;
using ProductionManagement.Application.Features.Production;
using ProductionManagement.Application.Features.Settings;
using ProductionManagement.Application.Features.Statistics;
using ProductionManagement.Domain.Services;
using ProductionManagement.Infrastructure.Persistence;
using ProductionManagement.Infrastructure.Security;
using ProductionManagement.Infrastructure.Time;

namespace ProductionManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddProductionManagement(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
                               ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IClock>(_ => new SystemClock(configuration["Business:TimeZone"]));

        // Luật phân bổ của Option 2 được đăng ký riêng để có thể thay đổi mà không đụng vào luồng
        // điều chỉnh.
        services.AddSingleton<IAutomaticAllocationStrategy, EvenDistributionAllocationStrategy>();

        services.AddScoped<AuthService>();
        services.AddScoped<OrderService>();
        services.AddScoped<ProductionDayService>();
        services.AddScoped<AdjustmentService>();
        services.AddScoped<SettingsService>();
        services.AddScoped<StatisticsService>();

        return services;
    }
}
