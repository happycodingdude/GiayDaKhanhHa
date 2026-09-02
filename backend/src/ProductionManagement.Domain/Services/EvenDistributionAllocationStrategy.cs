namespace ProductionManagement.Domain.Services;

/// <summary>
/// Option 2 — chia đều toàn bộ phần thiếu cho mọi ngày sản xuất còn lại.
/// Khi phần thiếu chia không hết, phần dư được cộng mỗi lần một đơn vị, bắt đầu từ ngày gần nhất
/// (Option 2 spec §4.5).
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
                ErrorCodes.NoEligibleTargetDay,
                "There is no remaining production day that can receive the shortage.");
        }

        var ordered = candidates.OrderBy(c => c.ProductionDate).ThenBy(c => c.ProductionPlanId).ToList();

        var baseShare = shortageQuantity / ordered.Count;
        var remainder = shortageQuantity % ordered.Count;

        var results = new List<AllocationResult>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var addOn = baseShare + (i < remainder ? 1 : 0);

            // add_on_quantity > 0 là ràng buộc CHECK của database, nên những ngày không nhận được gì
            // đơn giản là không nằm trong đề xuất.
            if (addOn == 0)
            {
                continue;
            }

            results.Add(new AllocationResult(ordered[i].ProductionPlanId, ordered[i].ProductionDate, addOn));
        }

        return results;
    }
}
