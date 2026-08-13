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
    /// Creates the Order and its initial production plans in a single transaction (Step 4 §19).
    /// </summary>
    public async Task<OrderDetailDto> CreateAsync(CreateOrderRequest request, CancellationToken ct = default)
    {
        var plans = (request.ProductionPlans ?? [])
            .Select(p => (p.ProductionDate, p.PlannedQuantity))
            .ToList();

        var now = clock.UtcNow;

        // All field and invariant validation lives in the aggregate root.
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
            // Another request inserted the same order code between the check and the insert.
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
            // Case-insensitive order-code search, expressed provider-agnostically.
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

        var records = await db.ProductionRecords.AsNoTracking()
            .Where(r => orderIds.Contains(r.OrderId))
            .Select(r => new { r.OrderId, r.ProductionDate, r.ActualQuantity })
            .ToListAsync(ct);

        var today = clock.Today;
        var plansByOrder = plans.ToLookup(p => p.OrderId);
        var recordsByOrder = records.ToLookup(r => r.OrderId);

        var items = orders.Select(order =>
        {
            var derived = OrderDerivedCalculator.Compute(
                order.Quantity,
                order.Status,
                order.DueDate,
                plansByOrder[order.Id].Select(p => (p.ProductionDate, p.PlannedQuantity, p.InitialPlannedQuantity)).ToList(),
                recordsByOrder[order.Id].Select(r => (r.ProductionDate, r.ActualQuantity)).ToList(),
                today);

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
                derived.IsOverdue);
        }).ToList();

        return new PagedResult<OrderListItemDto>(items, page, pageSize, totalCount);
    }

    public async Task<OrderDetailDto> GetByIdAsync(long orderId, CancellationToken ct = default)
    {
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId, ct)
                    ?? throw new NotFoundException(ErrorCodes.OrderNotFound, "Order was not found.");

        var derived = await ComputeDerivedAsync(order, ct);
        return ToDetailDto(order, derived);
    }

    /// <summary>
    /// Returns the daily production view: plan, actual and derived shortage/difference joined by
    /// Order + ProductionDate so the frontend does not have to combine several APIs (Step 4 §6).
    /// </summary>
    public async Task<ProductionPlanListDto> GetProductionPlansAsync(long orderId, CancellationToken ct = default)
    {
        if (!await db.Orders.AnyAsync(o => o.Id == orderId, ct))
        {
            throw new NotFoundException(ErrorCodes.OrderNotFound, "Order was not found.");
        }

        var plans = await db.ProductionPlans.AsNoTracking()
            .Where(p => p.OrderId == orderId)
            .OrderBy(p => p.ProductionDate)
            .ToListAsync(ct);

        var records = await db.ProductionRecords.AsNoTracking()
            .Where(r => r.OrderId == orderId)
            .ToListAsync(ct);

        var userNames = await GetUserDisplayNamesAsync(records.Select(r => r.UpdatedBy), ct);

        // A source plan may have at most one Applied adjustment at a time (Step 4 §12).
        var planIds = plans.Select(p => p.Id).ToList();
        var activeAdjustments = await db.PlanAdjustments.AsNoTracking()
            .Where(a => planIds.Contains(a.SourceProductionPlanId) && a.Status == AdjustmentStatus.Applied)
            .Select(a => new { a.Id, a.SourceProductionPlanId })
            .ToListAsync(ct);

        var activeBySourcePlan = activeAdjustments.ToDictionary(a => a.SourceProductionPlanId, a => a.Id);
        var recordsByDate = records.ToDictionary(r => r.ProductionDate);

        var items = plans.Select(plan =>
        {
            recordsByDate.TryGetValue(plan.ProductionDate, out var record);
            int? actual = record?.ActualQuantity;

            return new ProductionDayDto(
                Id: plan.Id,
                ProductionDate: plan.ProductionDate,
                InitialPlannedQuantity: plan.InitialPlannedQuantity,
                AddOnQuantity: plan.PlannedQuantity - plan.InitialPlannedQuantity,
                PlannedQuantity: plan.PlannedQuantity,
                ActualQuantity: actual,
                ProductionRecordId: record?.Id,
                ShortageQuantity: ProductionCalculations.Shortage(plan.PlannedQuantity, actual),
                Difference: ProductionCalculations.Difference(plan.PlannedQuantity, actual),
                HasActiveAdjustment: activeBySourcePlan.ContainsKey(plan.Id),
                ActiveAdjustmentId: activeBySourcePlan.TryGetValue(plan.Id, out var adjustmentId) ? adjustmentId : null,
                ActualEnteredBy: record is null ? null : userNames.GetValueOrDefault(record.UpdatedBy),
                ActualUpdatedAt: record?.UpdatedAt);
        }).ToList();

        return new ProductionPlanListDto(orderId, items);
    }

    internal async Task<OrderDerivedValues> ComputeDerivedAsync(Order order, CancellationToken ct)
    {
        var plans = await db.ProductionPlans.AsNoTracking()
            .Where(p => p.OrderId == order.Id)
            .Select(p => new { p.ProductionDate, p.PlannedQuantity, p.InitialPlannedQuantity })
            .ToListAsync(ct);

        var records = await db.ProductionRecords.AsNoTracking()
            .Where(r => r.OrderId == order.Id)
            .Select(r => new { r.ProductionDate, r.ActualQuantity })
            .ToListAsync(ct);

        return OrderDerivedCalculator.Compute(
            order.Quantity,
            order.Status,
            order.DueDate,
            plans.Select(p => (p.ProductionDate, p.PlannedQuantity, p.InitialPlannedQuantity)).ToList(),
            records.Select(r => (r.ProductionDate, r.ActualQuantity)).ToList(),
            clock.Today);
    }

    private async Task<Dictionary<long, string>> GetUserDisplayNamesAsync(
        IEnumerable<long> userIds, CancellationToken ct)
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
            order.CreatedAt,
            order.UpdatedAt);

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException?.GetType().GetProperty("SqlState")?.GetValue(ex.InnerException) as string == "23505";
}
