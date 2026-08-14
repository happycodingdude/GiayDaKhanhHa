using Microsoft.EntityFrameworkCore;
using ProductionManagement.Application.Abstractions;
using ProductionManagement.Application.Contracts;
using ProductionManagement.Domain;
using ProductionManagement.Domain.Entities;
using ProductionManagement.Domain.Services;

namespace ProductionManagement.Application.Features.Adjustments;

/// <summary>
/// Giữ cho khoản bù đã áp dụng luôn khớp với sản lượng thực tế mà nó dựa vào.
///
/// Phần thiếu của một ngày là giá trị suy ra: sửa thực tế của ngày đó là nó đổi theo. Khoản bù để
/// nguyên theo phần thiếu cũ sẽ tiếp tục lên sai số lượng cho các ngày đích, nên khi thực tế bị sửa
/// thì điều chỉnh đang hiệu lực được tính lại từ phần thiếu mới.
///
/// Điều chỉnh đã áp dụng là lịch sử bất biến (Step 4 §13) nên ở đây không bao giờ sửa nó: hệ thống
/// hoàn tác điều chỉnh đã cũ rồi áp dụng một cái mới, đúng cách sửa điều chỉnh mà tài liệu quy định.
/// Cả hai bản ghi đều tiếp tục hiển thị trong lịch sử.
///
/// Quyết định ban đầu của quản lý được giữ nguyên:
///   Manual     đúng (các) ngày đích họ đã chọn sẽ gánh phần thiếu mới.
///   Automatic  phần thiếu mới lại được chia đều cho các ngày còn lại.
/// </summary>
public sealed class ActiveAdjustmentRecalculator(
    IAppDbContext db,
    IClock clock,
    ICurrentUser currentUser,
    IAutomaticAllocationStrategy automaticAllocation)
{
    /// <summary>
    /// Phải gọi bên trong transaction đã thay đổi thực tế, sau khi thay đổi đó đã được lưu — phần
    /// thiếu mới được đọc lại từ database. Trả về null khi không có gì phải tính lại.
    /// </summary>
    public async Task<AdjustmentRecalculationDto?> RecalculateAsync(
        Guid orderId, DateOnly productionDate, CancellationToken ct = default)
    {
        var source = await db.ProductionPlans
            .FirstOrDefaultAsync(p => p.OrderId == orderId && p.ProductionDate == productionDate, ct);

        if (source is null)
        {
            return null;
        }

        // Mỗi ngày nguồn tối đa chỉ có một điều chỉnh ở trạng thái Applied (Step 4 §12).
        var adjustment = await db.PlanAdjustments
            .Include(a => a.Items)
            .FirstOrDefaultAsync(
                a => a.SourceProductionPlanId == source.Id && a.Status == AdjustmentStatus.Applied, ct);

        if (adjustment is null)
        {
            return null;
        }

        var actual = await db.ProductionRecords.AsNoTracking()
            .Where(r => r.OrderId == orderId && r.ProductionDate == productionDate)
            .Select(r => (int?)r.ActualQuantity)
            .FirstOrDefaultAsync(ct);

        var previousShortage = adjustment.ShortageQuantity;
        var newShortage = ProductionCalculations.Shortage(source.PlannedQuantity, actual);

        // Một lần sửa mà không làm đổi phần thiếu thì không được làm nhiễu lịch sử.
        if (newShortage == previousShortage)
        {
            return null;
        }

        var previousTargetIds = adjustment.Items.Select(i => i.ProductionPlanId).Distinct().ToList();

        var candidateIds = await db.ProductionPlans.AsNoTracking()
            .Where(AdjustmentRules.EligibleTarget(orderId, source.Id, source.ProductionDate, clock.Today))
            .Select(p => p.Id)
            .ToListAsync(ct);

        // Cùng giao thức khóa như luồng apply do quản lý kích hoạt: bên gọi đã khóa đơn hàng, sau đó
        // các kế hoạch được khóa theo thứ tự id tăng dần (Step 4 §18).
        var involvedIds = previousTargetIds.Concat(candidateIds).Distinct().ToList();
        await db.LockProductionPlansAsync(involvedIds, ct);

        var plans = await db.ProductionPlans
            .Where(p => involvedIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var now = clock.UtcNow;

        // Applied -> Reversed, và khoản bù được gỡ khỏi các kế hoạch đích.
        adjustment.Reverse(currentUser.UserId, now);
        foreach (var item in adjustment.Items)
        {
            plans[item.ProductionPlanId].RemoveAddOn(item.AddOnQuantity, now);
        }

        var replacement = newShortage <= 0
            ? null
            : BuildReplacement(adjustment, source, plans, previousTargetIds, candidateIds, newShortage, now);

        if (replacement is not null)
        {
            db.PlanAdjustments.Add(replacement.Value.Adjustment);

            foreach (var target in replacement.Value.Allocation)
            {
                plans[target.ProductionPlanId].AddOn(target.AddOnQuantity, now);
            }
        }

        // Lưu ở đây để bản thay thế có id trước khi được báo ngược về. Transaction do bên gọi sở hữu,
        // nên thao tác này vẫn all-or-nothing cùng với thực tế đã kích hoạt nó.
        await db.SaveChangesAsync(ct);

        var outcome = newShortage <= 0
            ? AdjustmentRecalculationOutcome.Removed
            : replacement is null
                ? AdjustmentRecalculationOutcome.Unhandled
                : AdjustmentRecalculationOutcome.Recalculated;

        return new AdjustmentRecalculationDto(
            Outcome: outcome,
            ReversedAdjustmentId: adjustment.Id,
            PreviousShortageQuantity: previousShortage,
            ShortageQuantity: Math.Max(newShortage, 0),
            AdjustmentType: adjustment.AdjustmentType.ToString(),
            AdjustmentId: replacement?.Adjustment.Id,
            Items: replacement is null
                ? []
                : replacement.Value.Allocation
                    .Select(a => new PlanAdjustmentItemDto(a.ProductionPlanId, a.ProductionDate, a.AddOnQuantity))
                    .ToList());
    }

    /// <summary>
    /// Dựng điều chỉnh thay thế cho cái đã cũ, hoặc null khi phần thiếu mới không còn chỗ nào để đặt.
    /// </summary>
    private (PlanAdjustment Adjustment, IReadOnlyList<AllocationResult> Allocation)? BuildReplacement(
        PlanAdjustment previous,
        ProductionPlan source,
        IReadOnlyDictionary<Guid, ProductionPlan> plans,
        IReadOnlyList<Guid> previousTargetIds,
        IReadOnlyList<Guid> candidateIds,
        int newShortage,
        DateTimeOffset now)
    {
        // Manual giữ đúng các ngày quản lý đã chọn. Ngày đã chọn mà nay rơi vào quá khứ thì không còn
        // hợp lệ và bị loại bỏ, chứ không bị điều chỉnh ngầm.
        var targetIds = previous.AdjustmentType == AdjustmentType.Automatic
            ? candidateIds
            : previousTargetIds.Where(candidateIds.Contains).ToList();

        if (targetIds.Count == 0)
        {
            return null;
        }

        // Danh sách ứng viên mang theo từng kế hoạch ở trạng thái sau khi đã gỡ khoản bù cũ.
        var candidates = targetIds
            .Select(id => new AllocationCandidate(id, plans[id].ProductionDate, plans[id].PlannedQuantity))
            .ToList();

        // Manual với đúng một ngày được chọn sẽ dồn toàn bộ phần thiếu vào ngày đó, đúng như luồng
        // Option 1 vẫn làm.
        var allocation = automaticAllocation.Allocate(newShortage, candidates);

        var adjustment = PlanAdjustment.Apply(
            source.Id,
            newShortage,
            previous.AdjustmentType,
            allocation.Select(a => (a.ProductionPlanId, a.AddOnQuantity)).ToList(),
            currentUser.UserId,
            now);

        return (adjustment, allocation);
    }
}
