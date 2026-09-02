using Microsoft.EntityFrameworkCore;
using ProductionManagement.Application.Abstractions;
using ProductionManagement.Application.Contracts;
using ProductionManagement.Domain;
using ProductionManagement.Domain.Entities;

namespace ProductionManagement.Application.Features.Settings;

/// <summary>
/// Cấu hình vận hành. Phase 1 chỉ có đúng một dòng, được tạo bởi bootstrap của ứng dụng.
///
/// Chu kỳ ghi nhận chỉ để nhắc trên màn hình nhập sản lượng — server không dùng nó để từ chối
/// request nào, và cấu hình không hồi tố dữ liệu đã ghi (CR-01 §6.8, N-10).
/// </summary>
public sealed class SettingsService(IAppDbContext db, IClock clock, ICurrentUser currentUser)
{
    public const int DefaultRecordingIntervalMinutes = 60;
    public const bool DefaultRemindBeforeDue = true;

    public async Task<SystemSettingsDto> GetAsync(CancellationToken ct = default)
        => ToDto(await LoadAsync(track: false, ct));

    public async Task<SystemSettingsDto> UpdateAsync(
        UpdateSystemSettingsRequest request, CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);

        var settings = await LoadAsync(track: true, ct);
        settings.Update(request.RecordingIntervalMinutes, request.RemindBeforeDue, currentUser.UserId, clock.UtcNow);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return ToDto(settings);
    }

    private async Task<SystemSettings> LoadAsync(bool track, CancellationToken ct)
    {
        var query = track ? db.SystemSettings : db.SystemSettings.AsNoTracking();

        return await query.OrderBy(s => s.Id).FirstOrDefaultAsync(ct)
               ?? throw new NotFoundException(
                   ErrorCodes.InternalError, "System settings have not been initialised.");
    }

    private static SystemSettingsDto ToDto(SystemSettings settings)
        => new(settings.RecordingIntervalMinutes, settings.RemindBeforeDue, settings.UpdatedAt);
}
