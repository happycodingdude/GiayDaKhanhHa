using Microsoft.EntityFrameworkCore;
using ProductionManagement.Application.Abstractions;
using ProductionManagement.Application.Common;
using ProductionManagement.Application.Contracts;
using ProductionManagement.Application.Features.Orders;
using ProductionManagement.Domain;
using ProductionManagement.Domain.Services;

namespace ProductionManagement.Application.Features.Statistics;

/// <summary>
/// Mọi số liệu thống kê đều suy ra từ dữ liệu gốc. Không có gì ở đây được lưu xuống (Step 4 §16).
///
/// Điểm dễ sai nhất sau CR-01: ô còn mở có sản lượng tạm tính nhưng KHÔNG có phần thiếu. Nhầm null
/// thành 0 sẽ khiến dashboard báo "đạt kế hoạch" cho ô đang sản xuất (CR-01 §14.8).
///
/// Sau CR-001, đơn <c>Pending</c> chưa có kế hoạch nên không tham gia bất kỳ chỉ số tiến độ nào; nó
/// chỉ được đếm riêng bằng <c>PendingOrderCount</c> (CR-001 §6.9).
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
            .Select(p => new
            {
                p.ProductionDate,
                p.ProductionLineId,
                p.PlannedQuantity,
                p.InitialPlannedQuantity,
            })
            .ToListAsync(ct);

        var snapshots = await db.SnapshotsForOrderAsync(orderId, ct);
        var snapshotsByCell = snapshots.ToDictionary(d => d.Key);

        var today = clock.Today;
        var cells = plans
            .Select(p => new PlanCell(p.ProductionDate, p.ProductionLineId, p.PlannedQuantity, p.InitialPlannedQuantity))
            .ToList();

        var derived = OrderDerivedCalculator.Compute(
            order.Quantity, order.Status, order.DueDate,
            cells, snapshots.Select(d => d.ToActualCell()).ToList(), today);

        // --- Đường xu hướng theo ngày, gộp mọi dây chuyền -------------------------------------
        var daily = new List<DailyStatisticsDto>();
        var cumulativePlan = 0;
        var cumulativeActual = 0;

        foreach (var group in plans.GroupBy(p => p.ProductionDate).OrderBy(g => g.Key))
        {
            var date = group.Key;
            var dayCells = group.ToList();

            var planned = dayCells.Sum(c => c.PlannedQuantity);
            var initialPlanned = dayCells.Sum(c => c.InitialPlannedQuantity);

            var dayDays = dayCells
                .Select(c => snapshotsByCell.GetValueOrDefault(new CellKey(date, c.ProductionLineId)))
                .ToList();

            var hasAnyRecord = dayDays.Any(d => d is not null);
            var actual = dayDays.Sum(d => d?.ActualQuantity ?? 0);

            // Ngày chỉ có số chính thức khi MỌI ô có kế hoạch của nó đã Xuất hàng. Còn một ô mở thì
            // chênh lệch/phần thiếu của cả ngày vẫn chưa chốt (CR-01 OV-5).
            var allClosed = dayCells.All(c =>
                snapshotsByCell.TryGetValue(new CellKey(date, c.ProductionLineId), out var d) && d.IsClosed);

            cumulativePlan += planned;
            cumulativeActual += actual;

            daily.Add(new DailyStatisticsDto(
                ProductionDate: date,
                InitialPlannedQuantity: initialPlanned,
                AddOnQuantity: planned - initialPlanned,
                PlannedQuantity: planned,
                ActualQuantity: hasAnyRecord ? actual : null,
                DayStatus: ProductionDayQueries.DisplayStatusOf(planned, date, allClosed, today),
                IsProvisional: hasAnyRecord && !allClosed,
                ClosedAt: allClosed ? dayDays.Max(d => d!.ClosedAt) : null,
                Difference: allClosed ? actual - planned : null,
                ShortageQuantity: allClosed ? Math.Max(planned - actual, 0) : null,
                CumulativePlan: cumulativePlan,
                CumulativeActual: cumulativeActual));
        }

        // --- Tách theo dây chuyền (CR-001 §6.9) ------------------------------------------------
        var lines = (await OrderQueries.ProductionLinesAsync(db, [orderId], ct))[orderId].ToList();

        var byLine = lines.Select(line =>
        {
            var linePlans = plans.Where(p => p.ProductionLineId == line.Id).ToList();
            var totalPlan = linePlans.Sum(p => p.PlannedQuantity);
            var totalActual = snapshots.Where(d => d.ProductionLineId == line.Id).Sum(d => d.ActualQuantity);

            // Phần thiếu cộng dồn từ các ô ĐÃ đóng: ô còn mở chưa có phần thiếu nào để cộng.
            var shortage = linePlans.Sum(p =>
                ProductionCalculations.Shortage(
                    p.PlannedQuantity,
                    snapshotsByCell.GetValueOrDefault(new CellKey(p.ProductionDate, line.Id))?.ClosedActualQuantity)
                ?? 0);

            return new OrderLineStatisticsDto(
                line.Id, line.Code, line.Name, line.AllocatedQuantity, totalPlan, totalActual, shortage);
        }).ToList();

        return new OrderStatisticsDto(
            order.Id,
            order.ShoeCode,
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
            daily,
            byLine);
    }

    public async Task<DashboardStatisticsDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var today = clock.Today;

        var orders = await db.Orders.AsNoTracking().ToListAsync(ct);
        var plans = await db.ProductionPlans.AsNoTracking()
            .Select(p => new
            {
                p.Id,
                p.OrderId,
                p.ProductionDate,
                p.ProductionLineId,
                p.PlannedQuantity,
                p.InitialPlannedQuantity,
            })
            .ToListAsync(ct);
        var snapshots = await db.AllSnapshotsAsync(ct);

        var lineCodes = await db.ProductionLines.AsNoTracking()
            .ToDictionaryAsync(l => l.Id, l => l.Code, ct);

        // Một kế hoạch nguồn tại một thời điểm chỉ có tối đa một điều chỉnh Applied (Step 4 §12);
        // phần thiếu đã có điều chỉnh thì không còn là việc phải xử lý.
        var handled = (await db.PlanAdjustments.AsNoTracking()
            .Where(a => a.Status == AdjustmentStatus.Applied)
            .Select(a => a.SourceProductionPlanId)
            .ToListAsync(ct)).ToHashSet();

        var plansByOrder = plans.ToLookup(p => p.OrderId);
        var snapshotsByOrder = snapshots.ToLookup(d => d.OrderId);

        var alerts = new List<DashboardAlertDto>();
        var trackedOrders = new List<DashboardOrderDto>();
        var todayProduction = new List<DashboardTodayProductionDto>();
        var unclosedPastCells = new List<DashboardUnclosedDayDto>();
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
            var orderSnapshots = snapshotsByOrder[order.Id].ToList();
            var byCell = orderSnapshots.ToDictionary(d => d.Key);

            var derived = OrderDerivedCalculator.Compute(
                order.Quantity,
                order.Status,
                order.DueDate,
                orderPlans
                    .Select(p => new PlanCell(p.ProductionDate, p.ProductionLineId, p.PlannedQuantity, p.InitialPlannedQuantity))
                    .ToList(),
                orderSnapshots.Select(d => d.ToActualCell()).ToList(),
                today);

            totalOrderQuantity += order.Quantity;
            totalActualQuantity += derived.TotalActual;
            totalRemainingQuantity += derived.Remaining;

            var todayCells = orderPlans.Where(p => p.ProductionDate == today).ToList();
            todayPlanned += todayCells.Sum(c => c.PlannedQuantity);

            foreach (var cell in todayCells)
            {
                if (byCell.TryGetValue(new CellKey(today, cell.ProductionLineId), out var snapshot))
                {
                    todayActual += snapshot.ActualQuantity;
                    todayHasAnyRecord = true;
                }
            }

            if (derived.ScheduleStatus == ScheduleStatus.Behind)
            {
                behindOrders++;
                alerts.Add(new DashboardAlertDto(
                    order.Id, order.ShoeCode, derived.BehindQuantity, derived.DaysRemaining,
                    derived.IsOverdue, order.DueDate!.Value));
            }

            // Đơn Pending chưa có kế hoạch nên không tham gia tính tiến độ (CR-001 §6.9).
            if (order.Status != OrderStatus.Incomplete)
            {
                continue;
            }

            foreach (var plan in orderPlans.Where(p => p.PlannedQuantity > 0).OrderBy(p => p.ProductionDate))
            {
                byCell.TryGetValue(new CellKey(plan.ProductionDate, plan.ProductionLineId), out var cell);
                var lineCode = lineCodes.GetValueOrDefault(plan.ProductionLineId, "—");

                // Đang sản xuất hôm nay: ô hôm nay có kế hoạch và chưa Xuất hàng (CR-01 §6.9).
                if (plan.ProductionDate == today && cell?.IsClosed != true)
                {
                    todayProduction.Add(new DashboardTodayProductionDto(
                        order.Id, order.ShoeCode, today, plan.ProductionLineId, lineCode,
                        plan.PlannedQuantity, cell?.ActualQuantity ?? 0, cell?.LastRecordedAt));
                }

                // Ô quá khứ chưa Xuất hàng — kể cả ô CHƯA có dòng production_days nào, tức là ô hoàn
                // toàn không nhập gì, đúng trường hợp cần cảnh báo nhất (CR-01 §14.5).
                if (plan.ProductionDate < today && cell?.IsClosed != true)
                {
                    unclosedPastCells.Add(new DashboardUnclosedDayDto(
                        order.Id, order.ShoeCode, plan.ProductionDate, plan.ProductionLineId, lineCode,
                        plan.PlannedQuantity, cell?.ActualQuantity ?? 0));
                }

                // Phần thiếu chỉ tồn tại ở ô đã Xuất hàng (CR-01 OV-5).
                if (cell?.IsClosed == true && !handled.Contains(plan.Id))
                {
                    var shortage = Math.Max(plan.PlannedQuantity - cell.ActualQuantity, 0);
                    if (shortage > 0)
                    {
                        openShortages.Add(new DashboardOpenShortageDto(
                            order.Id, order.ShoeCode, plan.Id, plan.ProductionDate,
                            plan.ProductionLineId, lineCode, shortage));
                    }
                }
            }

            // Timeline của dashboard chấm điểm từng ngày (gộp dây chuyền), nên phải kèm cả chuỗi
            // ngày sản xuất chứ không chỉ vị thế của hôm nay.
            var timeline = orderPlans
                .GroupBy(p => p.ProductionDate)
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    var planned = g.Sum(p => p.PlannedQuantity);
                    var hasAny = g.Any(p => byCell.ContainsKey(new CellKey(g.Key, p.ProductionLineId)));
                    var actual = g.Sum(p =>
                        byCell.GetValueOrDefault(new CellKey(g.Key, p.ProductionLineId))?.ActualQuantity ?? 0);
                    var allClosed = g.All(p =>
                        byCell.TryGetValue(new CellKey(g.Key, p.ProductionLineId), out var d) && d.IsClosed);

                    return new DashboardOrderDayDto(
                        g.Key,
                        planned,
                        hasAny ? actual : null,
                        ProductionDayQueries.DisplayStatusOf(planned, g.Key, allClosed, today));
                })
                .ToList();

            var todayEntry = timeline.FirstOrDefault(t => t.ProductionDate == today);
            var todayAllClosed = todayCells.Count > 0 && todayCells.All(c =>
                byCell.TryGetValue(new CellKey(today, c.ProductionLineId), out var d) && d.IsClosed);
            var todayOrderPlanned = todayCells.Sum(c => c.PlannedQuantity);
            var todayOrderActual = todayCells.Sum(c =>
                byCell.GetValueOrDefault(new CellKey(today, c.ProductionLineId))?.ActualQuantity ?? 0);

            trackedOrders.Add(new DashboardOrderDto(
                order.Id,
                order.ShoeCode,
                order.StartDate!.Value,
                order.DueDate!.Value,
                derived.ProgressPercentage,
                // Chênh lệch của hôm nay chỉ có nghĩa khi mọi ô của ngày đã chốt sổ.
                todayAllClosed ? todayOrderActual - todayOrderPlanned : null,
                todayCells.Count > 0,
                todayOrderPlanned,
                todayOrderActual,
                todayEntry?.DayStatus,
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
            PendingOrderCount: orders.Count(o => o.Status == OrderStatus.Pending),
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
                .ThenBy(o => o.ShoeCode)
                .ToList(),
            TodayProduction: todayProduction.OrderBy(t => t.ShoeCode).ThenBy(t => t.ProductionLineCode).ToList(),
            // Ô cũ nhất lên đầu: đó là ô đã treo lâu nhất.
            UnclosedPastCells: unclosedPastCells
                .OrderBy(d => d.ProductionDate).ThenBy(d => d.ShoeCode).ThenBy(d => d.ProductionLineCode).ToList(),
            OpenShortages: openShortages
                .OrderByDescending(s => s.ShortageQuantity).ThenBy(s => s.ProductionDate).ToList());
    }
}
