using ProductionManagement.Application.Abstractions;

namespace ProductionManagement.Infrastructure.Time;

/// <summary>
/// Audit timestamps are UTC. The business date ("today") is resolved in the configured business
/// timezone, because production dates are date-only values that must not shift with UTC offsets
/// (Step 3 §8, Step 5 §32).
/// </summary>
public sealed class SystemClock : IClock
{
    /// <summary>
    /// Nhà máy đặt tại Việt Nam, nên đây là business rule chứ không phải deployment config:
    /// mặc định nằm trong code để không lệ thuộc vào file cấu hình nào.
    /// </summary>
    public const string DefaultBusinessTimeZoneId = "Asia/Ho_Chi_Minh";

    private readonly TimeZoneInfo _businessTimeZone;

    public SystemClock(string? businessTimeZoneId)
    {
        _businessTimeZone = ResolveTimeZone(businessTimeZoneId);
    }

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateOnly Today => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, _businessTimeZone).DateTime);

    private static TimeZoneInfo ResolveTimeZone(string? id)
    {
        var timeZoneId = string.IsNullOrWhiteSpace(id) ? DefaultBusinessTimeZoneId : id;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
