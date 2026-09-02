namespace ProductionManagement.Domain.Entities;

/// <summary>
/// Cấu hình vận hành toàn hệ thống — Phase 1 chỉ có đúng một dòng, tạo bởi bootstrap của ứng dụng.
///
/// Chu kỳ ghi nhận chỉ dùng để nhắc trên màn hình nhập sản lượng. Server <b>không bao giờ</b> dùng
/// nó để từ chối request, và cấu hình không hồi tố dữ liệu đã ghi (CR-01 §6.8, N-10).
/// </summary>
public sealed class SystemSettings
{
    public const int MinIntervalMinutes = 5;
    public const int MaxIntervalMinutes = 480;

    private SystemSettings() { }

    public Guid Id { get; private set; }

    /// <summary>Bao lâu thì quản lý ghi nhận sản lượng một lần.</summary>
    public int RecordingIntervalMinutes { get; private set; }

    /// <summary>
    /// Có nhắc trước khi tới hạn ghi nhận hay không. Tắt thì chỉ nhắc sau khi đã quá hạn.
    /// Dù bật hay tắt, lời nhắc cũng không bao giờ chặn thao tác nào.
    /// </summary>
    public bool RemindBeforeDue { get; private set; }

    public Guid UpdatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static SystemSettings Create(
        int recordingIntervalMinutes, bool remindBeforeDue, Guid userId, DateTimeOffset now)
    {
        Validate(recordingIntervalMinutes);

        return new SystemSettings
        {
            Id = Guid.CreateVersion7(),
            RecordingIntervalMinutes = recordingIntervalMinutes,
            RemindBeforeDue = remindBeforeDue,
            UpdatedBy = userId,
            UpdatedAt = now
        };
    }

    public void Update(int recordingIntervalMinutes, bool remindBeforeDue, Guid userId, DateTimeOffset now)
    {
        Validate(recordingIntervalMinutes);

        RecordingIntervalMinutes = recordingIntervalMinutes;
        RemindBeforeDue = remindBeforeDue;
        UpdatedBy = userId;
        UpdatedAt = now;
    }

    private static void Validate(int recordingIntervalMinutes)
    {
        if (recordingIntervalMinutes is < MinIntervalMinutes or > MaxIntervalMinutes)
        {
            throw new ValidationException(
                "recordingIntervalMinutes", "OUT_OF_RANGE",
                $"The recording interval must be between {MinIntervalMinutes} and {MaxIntervalMinutes} minutes.");
        }
    }
}
