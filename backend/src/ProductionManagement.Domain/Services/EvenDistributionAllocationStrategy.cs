namespace ProductionManagement.Domain.Services;

/// <summary>
/// Option 2 — distributes the whole shortage evenly across every remaining production day.
/// When the shortage does not divide evenly, the remainder is added one unit at a time starting
/// from the nearest day (Option 2 spec §4.5).
/// </summary>
public sealed class EvenDistributionAllocationStrategy : IAutomaticAllocationStrategy
{
    public IReadOnlyList<AllocationResult> Allocate(int shortageQuantity, IReadOnlyList<AllocationCandidate> candidates)
    {
        if (shortageQuantity <= 0)
        {
            throw new BusinessRuleException(
                ErrorCodes.NoShortage, "There is no shortage to allocate.");
        }

        if (candidates.Count == 0)
        {
            throw new BusinessRuleException(
                ErrorCodes.NoEligibleTargetPlans,
                "There is no remaining production day that can receive the shortage.");
        }

        var ordered = candidates.OrderBy(c => c.ProductionDate).ThenBy(c => c.ProductionPlanId).ToList();

        var baseShare = shortageQuantity / ordered.Count;
        var remainder = shortageQuantity % ordered.Count;

        var results = new List<AllocationResult>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var addOn = baseShare + (i < remainder ? 1 : 0);

            // add_on_quantity > 0 is a database CHECK constraint, so days that would receive
            // nothing are simply not part of the proposal.
            if (addOn == 0)
            {
                continue;
            }

            results.Add(new AllocationResult(ordered[i].ProductionPlanId, ordered[i].ProductionDate, addOn));
        }

        return results;
    }
}
