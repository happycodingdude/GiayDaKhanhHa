namespace ProductionManagement.Application.Abstractions;

/// <summary>Password hashing. The database only ever stores <c>password_hash</c> (Step 3 §2.1).</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);
}

/// <summary>
/// The authenticated user, derived from the authentication context. The client never sends
/// audit user ids such as <c>createdBy</c> (Step 4 §3).
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid UserId { get; }
}

/// <summary>Clock abstraction so time-dependent business rules stay testable.</summary>
public interface IClock
{
    /// <summary>Audit timestamps are always stored in UTC (Step 3 §8).</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>
    /// The current business date. Business dates are date-only values with no timezone attached,
    /// so this is resolved against the configured business timezone rather than UTC.
    /// </summary>
    DateOnly Today { get; }
}
