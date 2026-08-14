namespace ProductionManagement.Domain.Services;

/// <summary>One proposed add-on for a target production plan.</summary>
public readonly record struct AllocationResult(Guid ProductionPlanId, DateOnly ProductionDate, int AddOnQuantity);

/// <summary>A production plan that is eligible to receive an add-on, ordered by date.</summary>
public readonly record struct AllocationCandidate(Guid ProductionPlanId, DateOnly ProductionDate, int CurrentPlannedQuantity);

/// <summary>
/// Automatic (Option 2) allocation. Kept behind an interface so the allocation rule can evolve
/// without touching the adjustment workflow (implementation prompt §9).
/// </summary>
public interface IAutomaticAllocationStrategy
{
    IReadOnlyList<AllocationResult> Allocate(int shortageQuantity, IReadOnlyList<AllocationCandidate> candidates);
}
