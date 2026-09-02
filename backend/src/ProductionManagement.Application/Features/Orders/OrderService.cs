using Microsoft.EntityFrameworkCore;
using ProductionManagement.Application.Abstractions;
using ProductionManagement.Application.Common;
using ProductionManagement.Application.Contracts;
using ProductionManagement.Domain;
using ProductionManagement.Domain.Entities;
using ProductionManagement.Domain.Services;

namespace ProductionManagement.Application.Features.Orders;

public sealed class OrderService(IAppDbContext db, IClock clock)
{
    /// <summary>
    /// Tạo Order và các kế hoạch sản xuất ban đầu của nó trong một transaction duy nhất (Step 4 §19).
    /// </summary>
    public async Task<OrderDetailDto> CreateAsync(CreateOrderRequest request, CancellationToken ct = default)
    {
        var plans = (request.ProductionPlans ?? [])
            .Select(p => (p.ProductionDate, p.PlannedQuantity))
            .ToList();

        var now = clock.UtcNow;

        // Toàn bộ việc kiểm tra field và bất biến nằm trong aggregate root.
        var order = Order.Create(request.OrderCode ?? string.Empty, request.Quantity, request.StartDate, request.DueDate, plans, now);

        var code = order.OrderCode;
        if (await db.Orders.AnyAsync(o => o.OrderCode == code, ct))
        {
            throw new ConflictException(
                ErrorCodes.OrderCodeAlreadyExists, $"Order code '{code}' is already in use.");
        }

        await using var transaction = await db.BeginTransactionAsync(ct);

        db.Orders.Add(order);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Một request khác đã chèn cùng mã đơn hàng vào giữa lúc kiểm tra và lúc insert.
            await transaction.RollbackAsync(ct);
            throw new ConflictException(
                ErrorCodes.OrderCodeAlreadyExists, $"Order code '{code}' is already in use.");
        }

        await transaction.CommitAsync(ct);

        var derived = OrderDerivedCalculator.Compute(
            order.Quantity,
            order.Status,
            order.DueDate,
            order.ProductionPlans.Select(p => (p.ProductionDate, p.PlannedQuantity, p.InitialPlannedQuantity)).ToList(),
            [],
            clock.Today);

        return ToDetailDto(order, derived);
    }

    public async Task<PagedResult<OrderListItemDto>> GetListAsync(
        string? status, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 20 : pageSize;

        var query = db.Orders.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var parsedStatus))
            {
                throw new ValidationException("status", "INVALID_VALUE", "Status must be 'Incomplete', 'Completed' or 'All'.");
            }

            query = query.Where(o => o.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
        // Tìm theo mã đơn hàng không phân biệt hoa thường, viết theo cách không phụ thuộc provider.
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(o => o.OrderCode.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(ct);

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var orderIds = orders.Select(o => o.Id).ToList();

        var plans = await db.ProductionPlans.AsNoTracking()
            .Where(p => orderIds.Contains(p.OrderId))
            .Select(p => new { p.OrderId, p.ProductionDate, p.PlannedQuantity, p.InitialPlannedQuantity })
            .ToListAsync(ct);

        var days = await db.SnapshotsForOrdersAsync(orderIds, ct);

        var today = clock.Today;
        var plansByOrder = plans.ToLookup(p => p.OrderId);
        var daysByOrder = days.ToLookup(d => d.OrderId);

        var items = orders.Select(order =>
        {
            var orderPlans = plansByOrder[order.Id].ToList();
            var orderDays = daysByOrder[order.Id].ToDictionary(d => d.ProductionDate);

            var derived = OrderDerivedCalculator.Compute(
                order.Quantity,
                order.Status,
                order.DueDate,
                orderPlans.Select(p => (p.ProductionDate, p.PlannedQuantity, p.InitialPlannedQuantity)).ToList(),
                orderDays.Values.Select(d => (d.ProductionDate, d.ActualQuantity, d.IsClosed)).ToList(),
                today);

            var todayPlan = orderPlans.FirstOrDefault(p => p.ProductionDate == today && p.PlannedQuantity > 0);
            orderDays.TryGetValue(today, out var todayDay);

            // Chỉ đơn chưa hoàn thành mới cần cảnh báo ngày treo: đơn đã xong thì phần thiếu còn
            // lại không cần xử lý nữa (CR-01 §14.6).
            var hasUnclosedPastDay = order.Status == OrderStatus.Incomplete
                && orderPlans.Any(p =>
                    p.PlannedQuantity > 0
                    && p.ProductionDate < today
                    && !(orderDays.TryGetValue(p.ProductionDate, out var day) && day.IsClosed));

            return new OrderListItemDto(
                order.Id,
                order.OrderCode,
                order.Quantity,
                order.StartDate,
                order.DueDate,
                order.Status.ToString(),
                derived.TotalActual,
                derived.Remaining,
                derived.TotalPlan,
                derived.ProgressPercentage,
                derived.ScheduleStatus,
                derived.BehindQuantity,
                derived.DaysRemaining,
                derived.IsOverdue,
                todayPlan?.PlannedQuantity,
                todayPlan is null ? null : todayDay?.ActualQuantity ?? 0,
                todayPlan is null
                    ? null
                    : ProductionDayQueries.DisplayStatusOf(
                        todayPlan.PlannedQuantity, today, todayDay?.IsClosed == true, today),
                hasUnclosedPastDay);
        }).ToList();

        return new PagedResult<OrderListItemDto>(items, page, pageSize, totalCount);
    }

    public async Task<OrderDetailDto> GetByIdAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId, ct)
                    ?? throw new NotFoundException(ErrorCodes.OrderNotFound, "Order was not found.");

        var derived = await ComputeDerivedAsync(order, ct);
        return ToDetailDto(order, derived);
    }

    /// <summary>
    /// Trả về bảng sản xuất theo ngày: kế hoạch, thực tế và phần thiếu/chênh lệch suy ra, ghép theo
    /// Order + ProductionDate để frontend không phải tự gộp nhiều API (Step 4 §6).
    /// </summary>
    public async Task<ProductionPlanListDto> GetProductionPlansAsync(Guid orderId, CancellationToken ct = default)
    {
        if (!await db.Orders.AnyAsync(o => o.Id == orderId, ct))
        {
            throw new NotFoundException(ErrorCodes.OrderNotFound, "Order was not found.");
        }

        var plans = await db.ProductionPlans.AsNoTracking()
            .Where(p => p.OrderId == orderId)
            .OrderBy(p => p.ProductionDate)
            .ToListAsync(ct);

        var days = await db.SnapshotsForOrderAsync(orderId, ct);

        var userNames = await GetUserDisplayNamesAsync(
            days.Where(d => d.LastRecordedBy.HasValue).Select(d => d.LastRecordedBy!.Value), ct);

        // Một kế hoạch nguồn tại một thời điểm chỉ có tối đa một điều chỉnh Applied (Step 4 §12).
        var planIds = plans.Select(p => p.Id).ToList();
        var activeAdjustments = await db.PlanAdjustments.AsNoTracking()
            .Where(a => planIds.Contains(a.SourceProductionPlanId) && a.Status == AdjustmentStatus.Applied)
            .Select(a => new { a.Id, a.SourceProductionPlanId })
            .ToListAsync(ct);

        var activeBySourcePlan = activeAdjustments.ToDictionary(a => a.SourceProductionPlanId, a => a.Id);
        var daysByDate = days.ToDictionary(d => d.ProductionDate);
        var today = clock.Today;

        var items = plans.Select(plan =>
        {
            daysByDate.TryGetValue(plan.ProductionDate, out var day);

            // Chưa ghi nhận lần nào thì để null, không bao giờ là 0. Ngày còn mở có sản lượng tạm
            // tính nhưng KHÔNG có phần thiếu và không có chênh lệch (CR-01 OV-5, N-07).
            int? actual = day is null ? null : day.ActualQuantity;

            return new ProductionDayDto(
                Id: plan.Id,
                ProductionDate: plan.ProductionDate,
                InitialPlannedQuantity: plan.InitialPlannedQuantity,
                AddOnQuantity: plan.PlannedQuantity - plan.InitialPlannedQuantity,
                PlannedQuantity: plan.PlannedQuantity,
                DayStatus: ProductionDayQueries.DisplayStatusOf(plan.PlannedQuantity, plan.ProductionDate, day?.IsClosed == true, today),
                ActualQuantity: actual,
                IsProvisional: day is not null && !day.IsClosed,
                ProductionDayId: day?.Id,
                ShortageQuantity: ProductionCalculations.Shortage(plan.PlannedQuantity, day?.ClosedActualQuantity),
                Difference: ProductionCalculations.Difference(plan.PlannedQuantity, day?.ClosedActualQuantity),
                ClosedAt: day?.ClosedAt,
                HasActiveAdjustment: activeBySourcePlan.ContainsKey(plan.Id),
                ActiveAdjustmentId: activeBySourcePlan.TryGetValue(plan.Id, out var adjustmentId) ? adjustmentId : null,
                LastRecordedBy: day?.LastRecordedBy is null ? null : userNames.GetValueOrDefault(day.LastRecordedBy.Value),
                LastRecordedAt: day?.LastRecordedAt);
        }).ToList();

        return new ProductionPlanListDto(orderId, items);
    }

    internal async Task<OrderDerivedValues> ComputeDerivedAsync(Order order, CancellationToken ct)
    {
        var plans = await db.ProductionPlans.AsNoTracking()
            .Where(p => p.OrderId == order.Id)
            .Select(p => new { p.ProductionDate, p.PlannedQuantity, p.InitialPlannedQuantity })
            .ToListAsync(ct);

        var days = await db.SnapshotsForOrderAsync(order.Id, ct);

        return OrderDerivedCalculator.Compute(
            order.Quantity,
            order.Status,
            order.DueDate,
            plans.Select(p => (p.ProductionDate, p.PlannedQuantity, p.InitialPlannedQuantity)).ToList(),
            days.Select(d => (d.ProductionDate, d.ActualQuantity, d.IsClosed)).ToList(),
            clock.Today);
    }

    private async Task<Dictionary<Guid, string>> GetUserDisplayNamesAsync(
        IEnumerable<Guid> userIds, CancellationToken ct)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
    }

    internal static OrderDetailDto ToDetailDto(Order order, OrderDerivedValues derived)
        => new(
            order.Id,
            order.OrderCode,
            order.Quantity,
            order.StartDate,
            order.DueDate,
            order.Status.ToString(),
            derived.TotalActual,
            derived.Remaining,
            derived.TotalPlan,
            derived.TotalInitialPlan,
            derived.ProgressPercentage,
            derived.ScheduleStatus,
            derived.BehindQuantity,
            derived.DaysRemaining,
            derived.IsOverdue,
            derived.IsPastDueDate,
            order.CreatedAt,
            order.UpdatedAt);

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException?.GetType().GetProperty("SqlState")?.GetValue(ex.InnerException) as string == "23505";
}
