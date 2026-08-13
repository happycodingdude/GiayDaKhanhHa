namespace ProductionManagement.Domain;

/// <summary>
/// Persisted as varchar + CHECK constraint (Step 3 §5). PostgreSQL native enums are not used.
/// </summary>
public enum UserStatus
{
    Active,
    Inactive
}

public enum OrderStatus
{
    Incomplete,
    Completed
}

public enum AdjustmentType
{
    /// <summary>Option 1 — the manager picks the target production day(s).</summary>
    Manual,

    /// <summary>Option 2 — the system distributes the shortage evenly across the remaining days.</summary>
    Automatic
}

public enum AdjustmentStatus
{
    Applied,
    Reversed
}
