using Microsoft.EntityFrameworkCore;
using ProductionManagement.Application.Abstractions;
using ProductionManagement.Application.Common;
using ProductionManagement.Application.Contracts;
using ProductionManagement.Domain;
using ProductionManagement.Domain.Services;

namespace ProductionManagement.Application.Features.Statistics;

/// <summary>
/// Mọi số liệu thống kê đều suy ra từ dữ liệu gốc. Không có gì ở đây được lưu xuống (Step 4 §16).
/// </summary>
public sealed class StatisticsService(IAppDbContext db, IClock clock)
{
    public async Task<OrderStatisticsDto> GetOrderStatisticsAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId, ct)
                    ?? throw new NotFoundException(ErrorCodes.OrderNotFound, "Order was not found.");

        var plans = await db.ProductionPlans.AsNoTracking()
            .Where(p => p.OrderId == orderId)
            .OrderBy(p => p.ProductionDate)
            .Select(p => new { p.ProductionDate, p.PlannedQuantity, p.InitialPlannedQuantity })
            .ToListAsync(ct);

        var records = await db.ProductionRecords.AsNoTracking()
            .Where(r => r.OrderId == orderId)
            .Select(r => new { r.ProductionDate, r.ActualQuantity })
            .ToListAsync(ct);

        var today = clock.Today;
        var derived = OrderDerivedCalculator.Compute(
            order.Quantity,
            order.Status,
            order.DueDate,
            plans.Select(p => (p.ProductionDate, p.PlannedQuantity, p.InitialPlannedQuantity)).ToList(),
            records.Select(r => (r.ProductionDate, r.ActualQuantity)).ToList(),
            today);

        var actualByDate = records.ToDictionary(r => r.ProductionDate, r => r.ActualQuantity);

        var daily = new List<DailyStatisticsDto>(plans.Count);
        var cumulativePlan = 0;
        var cumulativeActual = 0;

        foreach (var plan in plans)
        {
            int? actual = actualByDate.TryGetValue(plan.ProductionDate, out var value) ? value : null;

            cumulativePlan += plan.PlannedQuantity;
            cumulativeActual += actual ?? 0;

            daily.Add(new DailyStatisticsDto(
                ProductionDate: plan.ProductionDate,
                InitialPlannedQuantity: plan.InitialPlannedQuantity,
                AddOnQuantity: plan.PlannedQuantity - plan.InitialPlannedQuantity,
                PlannedQuantity: plan.PlannedQuantity,
                ActualQuantity: actual,
                Difference: ProductionCalculations.Difference(plan.PlannedQuantity, actual),
                ShortageQuantity: ProductionCalculations.Shortage(plan.PlannedQuantity, actual),
                CumulativePlan: cumulativePlan,
                CumulativeActual: cumulativeActual));
        }

        return new OrderStatisticsDto(
            order.Id,
            order.OrderCode,
            order.Quantity,
            derived.TotalActual,
            derived.Remaining,
            derived.TotalPlan,
            derived.TotalInitialPlan,
            derived.ProgressPercentage,
            derived.ScheduleStatus,
            derived.BehindQuantity,
            derived.DaysRemaining,
            derived.IsOverdue,
            daily);
    }

    public async Task<DashboardStatisticsDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var today = clock.Today;

        var orders = await db.Orders.AsNoTracking().ToListAsync(ct);
        var plans = await db.ProductionPlans.AsNoTracking()
            .Select(p => new { p.OrderId, p.ProductionDate, p.PlannedQuantity, p.InitialPlannedQuantity })
            .ToListAsync(ct);
        var records = await db.ProductionRecords.AsNoTracking()
            .Select(r => new { r.OrderId, r.ProductionDate, r.ActualQuantity })
            .ToListAsync(ct);

        var plansByOrder = plans.ToLookup(p => p.OrderId);
        var recordsByOrder = records.ToLookup(r => r.OrderId);

        var alerts = new List<DashboardAlertDto>();
        var trackedOrders = new List<DashboardOrderDto>();

        var totalOrderQuantity = 0;
        var totalActualQuantity = 0;
        var totalRemainingQuantity = 0;
        var behindOrders = 0;

        var todayPlanned = 0;
        var todayActual = 0;
        var todayHasAnyRecord = false;

        foreach (var order in orders)
        {
            var orderPlans = plansByOrder[order.Id].ToList();
            var orderRecords = recordsByOrder[order.Id].ToList();

            var derived = OrderDerivedCalculator.Compute(
                order.Quantity,
                order.Status,
                order.DueDate,
                orderPlans.Select(p => (p.ProductionDate, p.PlannedQuantity, p.InitialPlannedQuantity)).ToList(),
                orderRecords.Select(r => (r.ProductionDate, r.ActualQuantity)).ToList(),
                today);

            totalOrderQuantity += order.Quantity;
            totalActualQuantity += derived.TotalActual;
            totalRemainingQuantity += derived.Remaining;

            var todayPlan = orderPlans.FirstOrDefault(p => p.ProductionDate == today);
            var todayRecord = orderRecords.FirstOrDefault(r => r.ProductionDate == today);

            if (todayPlan is not null)
            {
                todayPlanned += todayPlan.PlannedQuantity;
            }

            if (todayRecord is not null)
            {
                todayActual += todayRecord.ActualQuantity;
                todayHasAnyRecord = true;
            }

            if (derived.ScheduleStatus == ScheduleStatus.Behind)
            {
                behindOrders++;
                alerts.Add(new DashboardAlertDto(
                    order.Id, order.OrderCode, derived.BehindQuantity, derived.DaysRemaining, derived.IsOverdue, order.DueDate));
            }

            if (order.Status == OrderStatus.Incomplete)
            {
                // Timeline của dashboard chấm điểm từng ngày, nên phải kèm cả chuỗi ngày sản xuất
                // chứ không chỉ vị thế của hôm nay. Bản ghi thực tế luôn gắn với một ngày có kế
                // hoạch (ProductionRecordService), nên duyệt theo kế hoạch là đã đủ.
                var actualByDate = orderRecords.ToDictionary(r => r.ProductionDate, r => r.ActualQuantity);
                var days = orderPlans
                    .OrderBy(p => p.ProductionDate)
                    .Select(p => new DashboardOrderDayDto(
                        p.ProductionDate,
                        p.PlannedQuantity,
                        actualByDate.TryGetValue(p.ProductionDate, out var actual) ? actual : null))
                    .ToList();

                trackedOrders.Add(new DashboardOrderDto(
                    order.Id,
                    order.OrderCode,
                    order.StartDate,
                    order.DueDate,
                    derived.ProgressPercentage,
                    todayPlan is null
                        ? null
                        : ProductionCalculations.Difference(todayPlan.PlannedQuantity, todayRecord?.ActualQuantity),
                    todayPlan is not null,
                    derived.Remaining,
                    derived.ScheduleStatus,
                    derived.BehindQuantity,
                    days));
            }
        }

        var todayDto = new DashboardTodayDto(
            PlannedQuantity: todayPlanned,
            ActualQuantity: todayActual,
            HasAnyActualEntered: todayHasAnyRecord,
            Difference: todayActual - todayPlanned,
            CompletionPercentage: ProductionCalculations.ProgressPercentage(todayPlanned, todayActual));

        return new DashboardStatisticsDto(
            Date: today,
            TotalOrders: orders.Count,
            IncompleteOrders: orders.Count(o => o.Status == OrderStatus.Incomplete),
            CompletedOrders: orders.Count(o => o.Status == OrderStatus.Completed),
            BehindOrders: behindOrders,
            TotalOrderQuantity: totalOrderQuantity,
            TotalActualQuantity: totalActualQuantity,
            TotalRemainingQuantity: totalRemainingQuantity,
            Today: todayDto,
            // Nghiêm trọng nhất lên đầu để quản lý thấy vấn đề tệ nhất trước (dashboard spec §7).
            Alerts: alerts.OrderByDescending(a => a.BehindQuantity).ThenBy(a => a.DaysRemaining).ToList(),
            TrackedOrders: trackedOrders.OrderBy(o => o.ScheduleStatus == ScheduleStatus.Behind ? 0 : 1)
                .ThenByDescending(o => o.BehindQuantity)
                .ThenBy(o => o.OrderCode)
                .ToList());
    }
}
