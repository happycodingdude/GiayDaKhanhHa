namespace ProductionManagement.Domain.Entities;

/// <summary>
/// Mô hình định danh của Phase 1. Nằm ngoài aggregate Order và đóng vai trò chủ thể cho các thao
/// tác sản xuất có audit (Step 3 §2). Phase 1 không có bảng Role/Permission.
/// </summary>
public sealed class User
{
    private User() { }

    public Guid Id { get; private set; }
    public string Username { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public UserStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsActive => Status == UserStatus.Active;

    public static User Create(string username, string passwordHash, string displayName, DateTimeOffset now)
    {
        return new User
        {
            Id = Guid.CreateVersion7(),
            Username = username.Trim(),
            PasswordHash = passwordHash,
            DisplayName = displayName.Trim(),
            Status = UserStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void ChangePassword(string passwordHash, DateTimeOffset now)
    {
        PasswordHash = passwordHash;
        UpdatedAt = now;
    }
}
