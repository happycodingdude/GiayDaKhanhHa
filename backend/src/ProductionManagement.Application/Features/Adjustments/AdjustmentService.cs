using Microsoft.EntityFrameworkCore;
using ProductionManagement.Application.Abstractions;
using ProductionManagement.Application.Common;
using ProductionManagement.Application.Contracts;
using ProductionManagement.Domain;
using ProductionManagement.Domain.Entities;
using ProductionManagement.Domain.Services;

namespace ProductionManagement.Application.Features.Adjustments;

/// <summary>
/// Xử lý phần thiếu. Preview không bao giờ lưu xuống; chỉ Apply mới tạo PlanAdjustment, và điều
/// chỉnh đã áp dụng là lịch sử bất biến, chỉ có thể chuyển sang Reversed (Step 4 §8–§13).
///
/// Sau CR-01, phần thiếu chỉ tồn tại ở ngày đã Xuất hàng, và ngày đã Xuất hàng thì sản lượng bị khoá
/// vĩnh viễn. Nghĩa là phần thiếu của ngày nguồn KHÔNG BAO GIỜ đổi sau khi khoản bù được áp dụng —
/// luồng "sửa thực tế làm khoản bù mất hiệu lực" không còn xảy ra được (CR-01 §3.1).
/// Reverse vẫn cần cho trường hợp quản lý chọn nhầm ngày bù và muốn làm lại; ADJUSTMENT_OUTDATED vẫn
/// cần vì phần thiếu có thể đã được xử lý bởi request khác, hoặc ngày đích vừa bị đóng.
/// </summary>
public sealed class AdjustmentService(
    IAppDbContext db,
    IClock clock,
    ICurrentUser currentUser,
    IAutomaticAllocationStrategy automaticAllocation)
{
    public async Task<AdjustmentPreviewDto> PreviewAsync(
        Guid productionPlanId, PreviewAdjustmentRequest request, CancellationToken ct = default)
    {
        var adjustmentType = ParseAdjustmentType(request.AdjustmentType);

        var source = await db.ProductionPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productionPlanId, ct)
                     ?? throw new NotFoundException(ErrorCodes.ProductionPlanNotFound, "Production plan was not found.");

        // Preview chỉ tồn tại để chuẩn bị cho Apply. Với đơn hàng quá hạn thì Apply không bao giờ
        // thành công, nên từ chối đề xuất ngay ở đây thay vì đưa ra rồi mới bác.
        var order = await GetOrderAsync(source.OrderId, ct);
        OrderMutationGuard.EnsureEditable(order, clock.Today);
        GuardOrderNotCompleted(order);

        var line = await GetLineAsync(source.ProductionLineId, ct);

        var (shortage, actual) = await GetShortageAsync(source, ct);
        if (shortage <= 0)
        {
            throw new BusinessRuleException(
                ErrorCodes.NoShortage, "This production day has no shortage to handle.");
        }

        await GuardNoActiveAdjustmentAsync(source.Id, ct);

        var candidates = await GetEligibleTargetsAsync(source, ct);
        var closedDates = await GetClosedDatesOnLineAsync(source.OrderId, source.ProductionLineId, ct);
        var allPlans = await db.ProductionPlans.AsNoTracking()
            .Where(p => p.OrderId == source.OrderId)
            .ToListAsync(ct);

        List<(Guid PlanId, DateOnly Date, int Current, int AddOn)> proposal;
        string? validationCode = null;
        string? validationMessage = null;

        if (adjustmentType == AdjustmentType.Automatic)
        {
            // Option 2 — hệ thống chia toàn bộ phần thiếu cho mọi ngày còn lại.
            var allocation = automaticAllocation.Allocate(
                shortage,
                candidates.Select(c => new AllocationCandidate(c.Id, c.ProductionDate, c.PlannedQuantity)).ToList());

            var byId = candidates.ToDictionary(c => c.Id);
            proposal = allocation
                .Select(a => (a.ProductionPlanId, a.ProductionDate, byId[a.ProductionPlanId].PlannedQuantity, a.AddOnQuantity))
                .ToList();
        }
        else
        {
            // Option 1 — các ngày đích do quản lý chọn, có kiểm tra nhưng không bao giờ sửa ngầm.
            var targets = request.Targets ?? [];
            var validation = ValidateManualTargets(targets, candidates, allPlans, closedDates, source, shortage);
            validationCode = validation.Code;
            validationMessage = validation.Message;

            // Chỉ trả về các ngày đích hợp lệ, nên preview không bao giờ hiện ra dòng mà server sẽ từ
            // chối. Lựa chọn không hợp lệ được báo qua thông báo validation.
            var byId = candidates.ToDictionary(c => c.Id);
            proposal = targets
                .Where(t => byId.ContainsKey(t.ProductionPlanId))
                .Select(t => (
                    t.ProductionPlanId,
                    byId[t.ProductionPlanId].ProductionDate,
                    byId[t.ProductionPlanId].PlannedQuantity,
                    t.AddOnQuantity))
                .ToList();
        }

        var items = proposal
            .OrderBy(p => p.Date)
            .Select(p => new AdjustmentPreviewItemDto(
                p.PlanId, p.Date, line.Id, line.Name, p.Current, p.AddOn, p.Current + p.AddOn))
            .ToList();

        return new AdjustmentPreviewDto(
            SourceProductionPlanId: source.Id,
            SourceProductionDate: source.ProductionDate,
            ProductionLineId: line.Id,
            ProductionLineCode: line.Code,
            ProductionLineName: line.Name,
            SourcePlannedQuantity: source.PlannedQuantity,
            SourceActualQuantity: actual,
            ShortageQuantity: shortage,
            AdjustmentType: adjustmentType.ToString(),
            Items: items,
            TotalAddOnQuantity: items.Sum(i => i.AddOnQuantity),
            Valid: validationCode is null,
            ValidationCode: validationCode,
            ValidationMessage: validationMessage);
    }

    public async Task<PlanAdjustmentDto> ApplyAsync(
        Guid productionPlanId, ApplyAdjustmentRequest request, CancellationToken ct = default)
    {
        var adjustmentType = ParseAdjustmentType(request.AdjustmentType);
        var targets = request.Targets ?? [];

        if (targets.Count == 0)
        {
            throw new ValidationException("targets", "REQUIRED", "At least one target production plan is required.");
        }

        await using var transaction = await db.BeginTransactionAsync(ct);

        var sourceInfo = await db.ProductionPlans.AsNoTracking()
            .Where(p => p.Id == productionPlanId)
            .Select(p => new { p.Id, p.OrderId })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(ErrorCodes.ProductionPlanNotFound, "Production plan was not found.");

        // Khóa đơn hàng trước (nó tuần tự hóa với thao tác tạo/sửa thực tế, thứ quyết định phần
        // thiếu), rồi tới các kế hoạch theo thứ tự id tăng dần (Step 4 §18).
        if (!await db.LockOrderAsync(sourceInfo.OrderId, ct))
        {
            throw new NotFoundException(ErrorCodes.OrderNotFound, "Order was not found.");
        }

        var order = await GetOrderAsync(sourceInfo.OrderId, ct);
        OrderMutationGuard.EnsureEditable(order, clock.Today);
        GuardOrderNotCompleted(order);

        var planIdsToLock = targets.Select(t => t.ProductionPlanId).Append(productionPlanId).Distinct().ToList();
        await db.LockProductionPlansAsync(planIdsToLock, ct);

        var source = await db.ProductionPlans.FirstAsync(p => p.Id == productionPlanId, ct);

        // Không bao giờ tin preview: tính lại phần thiếu hiện tại từ trạng thái sống (Step 4 §10).
        var (currentShortage, _) = await GetShortageAsync(source, ct);
        if (currentShortage <= 0 || currentShortage != request.ShortageQuantity)
        {
            throw new ConflictException(
                ErrorCodes.AdjustmentOutdated,
                "The adjustment proposal is no longer valid because the source production state has changed.");
        }

        await GuardNoActiveAdjustmentAsync(source.Id, ct);

        var candidates = await GetEligibleTargetsAsync(source, ct);
        var closedDates = await GetClosedDatesOnLineAsync(source.OrderId, source.ProductionLineId, ct);
        var allPlans = await db.ProductionPlans.AsNoTracking()
            .Where(p => p.OrderId == source.OrderId)
            .ToListAsync(ct);

        var validation = ValidateManualTargets(
            targets, candidates, allPlans, closedDates, source, currentShortage);
        if (validation.Code is not null)
        {
            throw new BusinessRuleException(validation.Code, validation.Message!);
        }

        if (adjustmentType == AdjustmentType.Automatic)
        {
            // Tính lại đề xuất tự động và bắt buộc cái client gửi lên phải khớp chính xác.
            // Server kiểm tra thứ quản lý gửi lên chứ không âm thầm thay thế nó.
            var expected = automaticAllocation.Allocate(
                currentShortage,
                candidates.Select(c => new AllocationCandidate(c.Id, c.ProductionDate, c.PlannedQuantity)).ToList());

            var expectedMap = expected.ToDictionary(e => e.ProductionPlanId, e => e.AddOnQuantity);
            var submittedMap = targets.ToDictionary(t => t.ProductionPlanId, t => t.AddOnQuantity);

            if (expectedMap.Count != submittedMap.Count ||
                expectedMap.Any(e => !submittedMap.TryGetValue(e.Key, out var qty) || qty != e.Value))
            {
                throw new ConflictException(
                    ErrorCodes.AdjustmentOutdated,
                    "The proposed allocation no longer matches the current production plan. Request a new preview.");
            }
        }

        var now = clock.UtcNow;

        var adjustment = PlanAdjustment.Apply(
            source.Id,
            currentShortage,
            adjustmentType,
            targets.Select(t => (t.ProductionPlanId, t.AddOnQuantity)).ToList(),
            currentUser.UserId,
            now);

        db.PlanAdjustments.Add(adjustment);

        // Tăng kế hoạch của các ngày đích. Không bao giờ giảm kế hoạch của ngày nào khác.
        var targetPlans = await db.ProductionPlans
            .Where(p => planIdsToLock.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        foreach (var target in targets)
        {
            targetPlans[target.ProductionPlanId].AddOn(target.AddOnQuantity, now);
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await GetAdjustmentDtoAsync(adjustment.Id, ct);
    }

    public async Task<PlanAdjustmentDto> ReverseAsync(Guid adjustmentId, CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);

        if (!await db.LockPlanAdjustmentAsync(adjustmentId, ct))
        {
            throw new NotFoundException(ErrorCodes.AdjustmentNotFound, "Plan adjustment was not found.");
        }

        var adjustment = await db.PlanAdjustments
            .Include(a => a.Items)
            .FirstAsync(a => a.Id == adjustmentId, ct);

        // plan_adjustments không có order_id; đơn hàng được truy ra qua kế hoạch sản xuất nguồn.
        var sourceOrderId = await db.ProductionPlans.AsNoTracking()
            .Where(p => p.Id == adjustment.SourceProductionPlanId)
            .Select(p => p.OrderId)
            .FirstAsync(ct);

        OrderMutationGuard.EnsureEditable(await GetOrderAsync(sourceOrderId, ct), clock.Today);

        var affectedPlanIds = adjustment.Items.Select(i => i.ProductionPlanId).Distinct().ToList();
        await db.LockProductionPlansAsync(affectedPlanIds, ct);

        // Chỉ Applied → Reversed. Điều chỉnh đã hoàn tác không bao giờ hoàn tác được lần nữa.
        adjustment.Reverse(currentUser.UserId, clock.UtcNow);

        var plans = await db.ProductionPlans
            .Where(p => affectedPlanIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var now = clock.UtcNow;
        foreach (var item in adjustment.Items)
        {
            plans[item.ProductionPlanId].RemoveAddOn(item.AddOnQuantity, now);
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await GetAdjustmentDtoAsync(adjustment.Id, ct);
    }

    public async Task<IReadOnlyList<PlanAdjustmentDto>> GetHistoryAsync(Guid orderId, CancellationToken ct = default)
    {
        if (!await db.Orders.AnyAsync(o => o.Id == orderId, ct))
        {
            throw new NotFoundException(ErrorCodes.OrderNotFound, "Order was not found.");
        }

        // Đơn hàng được truy ra qua kế hoạch sản xuất nguồn; plan_adjustments không có order_id.
        var adjustmentIds = await db.PlanAdjustments.AsNoTracking()
            .Where(a => db.ProductionPlans.Any(p => p.Id == a.SourceProductionPlanId && p.OrderId == orderId))
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Select(a => a.Id)
            .ToListAsync(ct);

        return await BuildAdjustmentDtosAsync(adjustmentIds, ct);
    }

    private async Task<Order> GetOrderAsync(Guid orderId, CancellationToken ct)
        => await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId, ct)
           ?? throw new NotFoundException(ErrorCodes.OrderNotFound, "Order was not found.");

    private async Task<PlanAdjustmentDto> GetAdjustmentDtoAsync(Guid adjustmentId, CancellationToken ct)
    {
        var dtos = await BuildAdjustmentDtosAsync([adjustmentId], ct);
        return dtos[0];
    }

    private async Task<IReadOnlyList<PlanAdjustmentDto>> BuildAdjustmentDtosAsync(
        IReadOnlyList<Guid> adjustmentIds, CancellationToken ct)
    {
        if (adjustmentIds.Count == 0)
        {
            return [];
        }

        var adjustments = await db.PlanAdjustments.AsNoTracking()
            .Where(a => adjustmentIds.Contains(a.Id))
            .Include(a => a.Items)
            .ToListAsync(ct);

        var planIds = adjustments
            .SelectMany(a => a.Items.Select(i => i.ProductionPlanId))
            .Concat(adjustments.Select(a => a.SourceProductionPlanId))
            .Distinct()
            .ToList();

        var planInfo = await db.ProductionPlans.AsNoTracking()
            .Where(p => planIds.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                p.ProductionDate,
                Line = new AdjustmentLineDto(p.ProductionLineId, p.ProductionLine.Code, p.ProductionLine.Name),
            })
            .ToDictionaryAsync(p => p.Id, ct);

        var userIds = adjustments
            .SelectMany(a => new[] { (Guid?)a.CreatedBy, a.AppliedBy, a.ReversedBy })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var userNames = userIds.Count == 0
            ? []
            : await db.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        var order = adjustmentIds.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);

        return adjustments
            .OrderBy(a => order[a.Id])
            .Select(a => new PlanAdjustmentDto(
                a.Id,
                a.SourceProductionPlanId,
                planInfo.GetValueOrDefault(a.SourceProductionPlanId)?.ProductionDate ?? default,
                // Mọi ô của một lần bù luôn cùng một dây chuyền, nên dây chuyền của ô nguồn là dây
                // chuyền của cả lần bù (CR-001 BR-N12).
                planInfo.GetValueOrDefault(a.SourceProductionPlanId)?.Line
                    ?? new AdjustmentLineDto(Guid.Empty, "—", "—"),
                a.ShortageQuantity,
                a.AdjustmentType.ToString(),
                a.Status.ToString(),
                a.Items
                    .Select(i => new PlanAdjustmentItemDto(
                        i.ProductionPlanId,
                        planInfo.GetValueOrDefault(i.ProductionPlanId)?.ProductionDate ?? default,
                        i.AddOnQuantity))
                    .OrderBy(i => i.ProductionDate)
                    .ToList(),
                userNames.GetValueOrDefault(a.CreatedBy, "—"),
                a.AppliedBy.HasValue ? userNames.GetValueOrDefault(a.AppliedBy.Value) : null,
                a.ReversedBy.HasValue ? userNames.GetValueOrDefault(a.ReversedBy.Value) : null,
                a.CreatedAt,
                a.AppliedAt,
                a.ReversedAt))
            .ToList();
    }

    /// <summary>
    /// Phần thiếu của ngày nguồn. Chỉ tồn tại khi ngày đó đã Xuất hàng: ngày còn mở chưa có con số
    /// chính thức nào để bù (CR-01 OV-5, AC-13).
    /// </summary>
    private async Task<(int Shortage, int Actual)> GetShortageAsync(ProductionPlan source, CancellationToken ct)
    {
        var day = await db.ProductionDays.AsNoTracking()
            .Where(d => d.OrderId == source.OrderId
                        && d.ProductionDate == source.ProductionDate
                        && d.ProductionLineId == source.ProductionLineId)
            .Select(d => new { d.Status, d.ActualQuantity })
            .FirstOrDefaultAsync(ct);

        if (day is null || day.Status != ProductionDayStatus.Closed)
        {
            throw new BusinessRuleException(
                ErrorCodes.SourceDayNotClosed,
                "This production cell has not been closed yet, so it has no confirmed shortage to handle.");
        }

        var actual = day.ActualQuantity!.Value;
        return (Math.Max(source.PlannedQuantity - actual, 0), actual);
    }

    /// <summary>
    /// Các ngày ĐÃ Xuất hàng của một dây chuyền — không ngày nào trong số đó nhận được khoản bù.
    /// Lọc theo dây chuyền là bắt buộc: cùng một ngày, dây chuyền A có thể đã đóng còn dây chuyền B
    /// thì chưa (CR-001 §2 QĐ-3).
    /// </summary>
    private async Task<List<DateOnly>> GetClosedDatesOnLineAsync(
        Guid orderId, Guid productionLineId, CancellationToken ct)
        => await db.ProductionDays.AsNoTracking()
            .Where(d => d.OrderId == orderId
                        && d.ProductionLineId == productionLineId
                        && d.Status == ProductionDayStatus.Closed)
            .Select(d => d.ProductionDate)
            .ToListAsync(ct);

    private async Task<ProductionLine> GetLineAsync(Guid productionLineId, CancellationToken ct)
        => await db.ProductionLines.AsNoTracking().FirstOrDefaultAsync(l => l.Id == productionLineId, ct)
           ?? throw new NotFoundException(ErrorCodes.ProductionLineNotFound, "Production line was not found.");

    /// <summary>
    /// Đơn đã hoàn thành thì phần thiếu còn lại không cần xử lý nữa: các ngày phía sau chỉ còn chờ
    /// được đóng cho sạch sổ (CR-01 §14.6).
    /// </summary>
    private static void GuardOrderNotCompleted(Order order)
    {
        if (order.IsCompleted)
        {
            throw new BusinessRuleException(
                ErrorCodes.OrderAlreadyCompleted,
                "This order is already completed, so its remaining shortage no longer needs handling.");
        }
    }

    private async Task GuardNoActiveAdjustmentAsync(Guid sourceProductionPlanId, CancellationToken ct)
    {
        // Mỗi kế hoạch nguồn tối đa một điều chỉnh Applied (Step 4 §12). Điều này cũng khiến việc
        // apply trùng sau khi retry mạng là không thể, mà không cần bảng idempotency.
        var hasActive = await db.PlanAdjustments
            .AnyAsync(a => a.SourceProductionPlanId == sourceProductionPlanId && a.Status == AdjustmentStatus.Applied, ct);

        if (hasActive)
        {
            throw new ConflictException(
                ErrorCodes.ActiveAdjustmentExists,
                "This production day already has an active adjustment. Reverse it before creating a new one.");
        }
    }

    /// <summary>
    /// Các ô được nhận khoản bù: cùng dây chuyền với ô nguồn, nằm sau ngày thiếu và không thuộc quá
    /// khứ. Điều chỉnh một ngày đã qua là viết lại lịch sử (master summary §8 Rule 7, §11);
    /// bù sang dây chuyền khác bị cấm ở Phase này (CR-001 BR-N12).
    /// </summary>
    private async Task<List<ProductionPlan>> GetEligibleTargetsAsync(ProductionPlan source, CancellationToken ct)
    {
        var closedDates = await GetClosedDatesOnLineAsync(source.OrderId, source.ProductionLineId, ct);

        var candidates = await db.ProductionPlans.AsNoTracking()
            .Where(AdjustmentRules.EligibleTarget(
                source.OrderId, source.Id, source.ProductionLineId, source.ProductionDate, clock.Today, closedDates))
            .OrderBy(p => p.ProductionDate)
            .ToListAsync(ct);

        // Trường hợp biên: ngày cuối của dây chuyền bị thiếu và không còn ô nào phía sau chưa đóng
        // TRÊN CHÍNH DÂY CHUYỀN ĐÓ (CR-01 §6.7 AC-15, CR-001 BR-N12).
        if (candidates.Count == 0)
        {
            throw new BusinessRuleException(
                ErrorCodes.NoEligibleTargetDay,
                "This production line has no remaining production day that can absorb the shortage.");
        }

        return candidates;
    }

    private (string? Code, string? Message) ValidateManualTargets(
        IReadOnlyList<AdjustmentTargetRequest> targets,
        IReadOnlyList<ProductionPlan> candidates,
        IReadOnlyList<ProductionPlan> allPlans,
        IReadOnlyCollection<DateOnly> closedDates,
        ProductionPlan source,
        int shortage)
    {
        if (targets.Count == 0)
        {
            return (ErrorCodes.InvalidAdjustmentTarget, "Select at least one production day to absorb the shortage.");
        }

        var eligibleIds = candidates.Select(c => c.Id).ToHashSet();
        var plansById = allPlans.ToDictionary(p => p.Id);
        var seen = new HashSet<Guid>();

        foreach (var target in targets)
        {
            if (target.AddOnQuantity <= 0)
            {
                return (ErrorCodes.InvalidAdjustmentTarget, "Each add-on quantity must be greater than zero.");
            }

            if (!eligibleIds.Contains(target.ProductionPlanId))
            {
                // Nói đúng lý do bị loại thay vì một câu chung chung: UI hiển thị nguyên nhân ngay
                // cạnh ngày mà quản lý vừa chọn (CR-01 §6.7, §8).
                if (!plansById.TryGetValue(target.ProductionPlanId, out var plan))
                {
                    return (ErrorCodes.InvalidAdjustmentTarget,
                        "A selected production day does not belong to this order.");
                }

                var rejection = AdjustmentRules.RejectionFor(
                    plan.ProductionLineId, source.ProductionLineId,
                    plan.ProductionDate, source.ProductionDate, clock.Today,
                    // closedDates chỉ chứa ngày của dây chuyền nguồn, nên chỉ tra cứu được khi ô
                    // đích cùng dây chuyền — trường hợp khác dây chuyền đã bị RejectionFor bắt trước.
                    plan.ProductionLineId == source.ProductionLineId
                    && closedDates.Contains(plan.ProductionDate));

                return rejection ?? (ErrorCodes.InvalidAdjustmentTarget,
                    "A selected production day cannot receive this add-on.");
            }

            if (!seen.Add(target.ProductionPlanId))
            {
                return (ErrorCodes.DuplicateAdjustmentTarget,
                    "The same production day cannot appear twice in one adjustment.");
            }
        }

        var total = targets.Sum(t => (long)t.AddOnQuantity);
        if (total != shortage)
        {
            return (ErrorCodes.AdjustmentTotalMismatch,
                $"The total add-on quantity ({total}) must equal the shortage quantity ({shortage}).");
        }

        return (null, null);
    }

    private static AdjustmentType ParseAdjustmentType(string? value)
    {
        if (!Enum.TryParse<AdjustmentType>(value, ignoreCase: true, out var parsed))
        {
            throw new ValidationException(
                "adjustmentType", "INVALID_VALUE", "Adjustment type must be 'Manual' or 'Automatic'.");
        }

        return parsed;
    }
}
