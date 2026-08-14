namespace ProductionManagement.Application.Contracts;

/// <summary>Khung phân trang phía server dùng cho danh sách đơn hàng (Step 5 §13).</summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>
/// Tình trạng tiến độ, chủ đích tách khỏi trạng thái đơn hàng. "Chậm tiến độ" không phải là một
/// trạng thái đơn hàng (order list spec §5).
/// </summary>
public enum ScheduleStatus
{
    OnSchedule,
    Behind,
    Completed
}
