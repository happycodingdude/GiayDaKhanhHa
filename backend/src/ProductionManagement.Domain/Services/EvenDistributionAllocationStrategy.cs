namespace ProductionManagement.Domain.Services;

/// <summary>
/// Option 2 — chia đều toàn bộ phần thiếu cho mọi ô sản xuất còn lại CỦA CÙNG MỘT DÂY CHUYỀN.
/// Danh sách ứng viên do bên gọi lọc theo dây chuyền; ở đây chỉ còn thuần bài toán chia đều
/// (CR-001 BR-N13).
/// Khi phần thiếu chia không hết, phần dư được cộng mỗi lần một đơn vị, bắt đầu từ ngày gần nhất
/// (Option 2 spec §4.5, CR-001 §6.6b).
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

        // Cùng một quy tắc chia dư với hai tầng phân bổ khi lập tiến độ (CR-001 §6.6b).
        var shares = EvenDistribution.Split(shortageQuantity, ordered.Count);

        var results = new List<AllocationResult>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var addOn = shares[i];

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
