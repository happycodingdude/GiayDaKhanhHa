namespace ProductionManagement.Application.Contracts;

/// <summary>Server-side pagination envelope used by the order list (Step 5 §13).</summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>
/// Progress condition, deliberately separate from the order status. "Behind schedule" is not an
/// order status (order list spec §5).
/// </summary>
public enum ScheduleStatus
{
    OnSchedule,
    Behind,
    Completed
}
