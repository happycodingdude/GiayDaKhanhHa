namespace ProductionManagement.Domain.Services;

/// <summary>Một khoản bù được đề xuất cho một kế hoạch sản xuất đích.</summary>
public readonly record struct AllocationResult(Guid ProductionPlanId, DateOnly ProductionDate, int AddOnQuantity);

/// <summary>Một kế hoạch sản xuất đủ điều kiện nhận khoản bù, sắp xếp theo ngày.</summary>
public readonly record struct AllocationCandidate(Guid ProductionPlanId, DateOnly ProductionDate, int CurrentPlannedQuantity);

/// <summary>
/// Phân bổ tự động (Option 2). Đặt sau một interface để luật phân bổ có thể thay đổi mà không phải
/// đụng vào luồng điều chỉnh (implementation prompt §9).
/// </summary>
public interface IAutomaticAllocationStrategy
{
    IReadOnlyList<AllocationResult> Allocate(int shortageQuantity, IReadOnlyList<AllocationCandidate> candidates);
}
