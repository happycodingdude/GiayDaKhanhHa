using Microsoft.EntityFrameworkCore;
using ProductionManagement.Application.Abstractions;
using ProductionManagement.Application.Common;
using ProductionManagement.Application.Contracts;
using ProductionManagement.Domain;
using ProductionManagement.Domain.Services;

namespace ProductionManagement.Application.Features.Statistics;

/// <summary>
/// Mọi số liệu thống kê đều suy ra từ dữ liệu gốc. Không có gì ở đây được lưu xuống (Step 4 §16).
///
/// Điểm dễ sai nhất sau CR-01: ngày còn mở có sản lượng tạm tính nhưng KHÔNG có phần thiếu. Nhầm
/// null thành 0 sẽ khiến dashboard báo "đạt kế hoạch" cho ngày đang sản xuất (CR-01 §14.8).
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

        var days = await db.SnapshotsForOrderAsync(orderId, ct);

        var today = clock.Today;
        var derived = OrderDerivedCalculator.Compute(
            order.Quantity,
            order.Status,
            order.DueDate,
            plans.Select(p => (p.ProductionDate, p.PlannedQuantity, p.InitialPlannedQuantity)).ToList(),
            days.Select(d => (d.ProductionDate, d.ActualQuantity, d.IsClosed)).ToList(),
            today);

        var daysByDate = days.ToDictionary(d => d.ProductionDate);

        var daily = new List<DailyStatisticsDto>(plans.Count);
        var cumulativePlan = 0;
        var cumulativeActual = 0;

        foreach (var plan in plans)
        {
            daysByDate.TryGetValue(plan.ProductionDate, out var day);

            cumulativePlan += plan.PlannedQuantity;
            cumulativeActual += day?.ActualQuantity ?? 0;

            daily.Add(new DailyStatisticsDto(
                ProductionDate: plan.ProductionDate,
                InitialPlannedQuantity: plan.InitialPlannedQuantity,
                AddOnQuantity: plan.PlannedQuantity - plan.InitialPlannedQuantity,
                PlannedQuantity: plan.PlannedQuantity,
                ActualQuantity: day?.ActualQuantity,
                DayStatus: ProductionDayQueries.DisplayStatusOf(
                    plan.PlannedQuantity, plan.ProductionDate, day?.IsClosed == true, today),
                IsProvisional: day is not null && !day.IsClosed,
                ClosedAt: day?.ClosedAt,
                Difference: ProductionCalculations.Difference(plan.PlannedQuantity, day?.ClosedActualQuantity),
                ShortageQuantity: ProductionCalculations.Shortage(plan.PlannedQuantity, day?.ClosedActualQuantity),
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
            .Select(p => new { p.Id, p.OrderId, p.ProductionDate, p.PlannedQuantity, p.InitialPlannedQuantity })
            .ToListAsync(ct);
        var days = await db.AllSnapshotsAsync(ct);

        // Một kế hoạch nguồn tại một thời điểm chỉ có tối đa một điều chỉnh Applied (Step 4 §12);
        // phần thiếu đã có điều chỉnh thì không còn là việc phải xử lý.
        var handledPlanIds = await db.PlanAdjustments.AsNoTracking()
            .Where(a => a.Status == AdjustmentStatus.Applied)
            .Select(a => a.SourceProductionPlanId)
            .ToListAsync(ct);
        var handled = handledPlanIds.ToHashSet();

        var plansByOrder = plans.ToLookup(p => p.OrderId);
        var daysByOrder = days.ToLookup(d => d.OrderId);

        var alerts = new List<DashboardAlertDto>();
        var trackedOrders = new List<DashboardOrderDto>();
        var todayProduction = new List<DashboardTodayProductionDto>();
        var unclosedPastDays = new List<DashboardUnclosedDayDto>();
        var openShortages = new List<DashboardOpenShortageDto>();

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
            var orderDays = daysByOrder[order.Id].ToDictionary(d => d.ProductionDate);

            var derived = OrderDerivedCalculator.Compute(
                order.Quantity,
                order.Status,
                order.DueDate,
                orderPlans.Select(p => (p.ProductionDate, p.PlannedQuantity, p.InitialPlannedQuantity)).ToList(),
                orderDays.Values.Select(d => (d.ProductionDate, d.ActualQuantity, d.IsClosed)).ToList(),
                today);

            totalOrderQuantity += order.Quantity;
            totalActualQuantity += derived.TotalActual;
            totalRemainingQuantity += derived.Remaining;

            var todayPlan = orderPlans.FirstOrDefault(p => p.ProductionDate == today);
            orderDays.TryGetValue(today, out var todayDay);

            if (todayPlan is not null)
            {
                todayPlanned += todayPlan.PlannedQuantity;
            }

            if (todayDay is not null)
            {
                todayActual += todayDay.ActualQuantity;
                todayHasAnyRecord = true;
            }

            if (derived.ScheduleStatus == ScheduleStatus.Behind)
            {
                behindOrders++;
                alerts.Add(new DashboardAlertDto(
                    order.Id, order.OrderCode, derived.BehindQuantity, derived.DaysRemaining, derived.IsOverdue, order.DueDate));
            }

            if (order.Status != OrderStatus.Incomplete)
            {
                continue;
            }

            // Đang sản xuất hôm nay: ngày hôm nay có kế hoạch và chưa Xuất hàng (CR-01 §6.9).
            if (todayPlan is { PlannedQuantity: > 0 } && todayDay?.IsClosed != true)
            {
                todayProduction.Add(new DashboardTodayProductionDto(
                    order.Id, order.OrderCode, today, todayPlan.PlannedQuantity,
                    todayDay?.ActualQuantity ?? 0, todayDay?.LastRecordedAt));
            }

            foreach (var plan in orderPlans.Where(p => p.PlannedQuantity > 0).OrderBy(p => p.ProductionDate))
            {
                orderDays.TryGetValue(plan.ProductionDate, out var day);

                // Ngày quá khứ chưa Xuất hàng — kể cả ngày CHƯA có dòng production_days nào, tức là
                // ngày hoàn toàn không nhập gì, đúng trường hợp cần cảnh báo nhất (CR-01 §14.5).
                if (plan.ProductionDate < today && day?.IsClosed != true)
                {
                    unclosedPastDays.Add(new DashboardUnclosedDayDto(
                        order.Id, order.OrderCode, plan.ProductionDate, plan.PlannedQuantity,
                        day?.ActualQuantity ?? 0));
                }

                // Phần thiếu chỉ tồn tại ở ngày đã Xuất hàng (CR-01 OV-5).
                if (day?.IsClosed == true && !handled.Contains(plan.Id))
                {
                    var shortage = Math.Max(plan.PlannedQuantity - day.ActualQuantity, 0);
                    if (shortage > 0)
                    {
                        openShortages.Add(new DashboardOpenShortageDto(
                            order.Id, order.OrderCode, plan.Id, plan.ProductionDate, shortage));
                    }
                }
            }

            // Timeline của dashboard chấm điểm từng ngày, nên phải kèm cả chuỗi ngày sản xuất chứ
            // không chỉ vị thế của hôm nay.
            var timeline = orderPlans
                .OrderBy(p => p.ProductionDate)
                .Select(p =>
                {
                    orderDays.TryGetValue(p.ProductionDate, out var day);
                    return new DashboardOrderDayDto(
                        p.ProductionDate,
                        p.PlannedQuantity,
                        day?.ActualQuantity,
                        ProductionDayQueries.DisplayStatusOf(
                            p.PlannedQuantity, p.ProductionDate, day?.IsClosed == true, today));
                })
                .ToList();

            trackedOrders.Add(new DashboardOrderDto(
                order.Id,
                order.OrderCode,
                order.StartDate,
                order.DueDate,
                derived.ProgressPercentage,
                // Chênh lệch của hôm nay chỉ có nghĩa khi ngày đã chốt sổ.
                todayPlan is null
                    ? null
                    : ProductionCalculations.Difference(todayPlan.PlannedQuantity, todayDay?.ClosedActualQuantity),
                todayPlan is not null,
                todayPlan?.PlannedQuantity ?? 0,
                todayDay?.ActualQuantity ?? 0,
                todayPlan is null
                    ? null
                    : ProductionDayQueries.DisplayStatusOf(
                        todayPlan.PlannedQuantity, today, todayDay?.IsClosed == true, today),
                derived.Remaining,
                derived.ScheduleStatus,
                derived.BehindQuantity,
                timeline));
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
                .ToList(),
            TodayProduction: todayProduction.OrderBy(t => t.OrderCode).ToList(),
            // Ngày cũ nhất lên đầu: đó là ngày đã treo lâu nhất.
            UnclosedPastDays: unclosedPastDays.OrderBy(d => d.ProductionDate).ThenBy(d => d.OrderCode).ToList(),
            OpenShortages: openShortages.OrderByDescending(s => s.ShortageQuantity).ThenBy(s => s.ProductionDate).ToList());
    }
}
