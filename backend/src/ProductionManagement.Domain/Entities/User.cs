namespace ProductionManagement.Domain.Entities;

/// <summary>
/// Phase 1 identity model. Lives outside the Order aggregate and acts as the actor for
/// audited production operations (Step 3 §2). No Role/Permission tables in Phase 1.
/// </summary>
public sealed class User
{
    private User() { }

    public long Id { get; private set; }
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
