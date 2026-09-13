using Microsoft.EntityFrameworkCore;
using ProductionManagement.Application.Abstractions;
using ProductionManagement.Application.Common;
using ProductionManagement.Application.Contracts;
using ProductionManagement.Application.Features.Orders;
using ProductionManagement.Domain;
using ProductionManagement.Domain.Entities;
using ProductionManagement.Domain.Services;

namespace ProductionManagement.Application.Features.Production;

/// <summary>
/// Ghi nhận sản lượng nhiều lần trong ngày và chốt sổ (Xuất hàng) — CR-01, áp lên ma trận CR-001.
///
/// Sau CR-001, một "ngày sản xuất" luôn gắn với một dây chuyền: đó chính là một ô của ma trận
/// ngày × dây chuyền. Sản lượng thực tế vẫn là số cộng thêm — một ô có N lần ghi nhận, tổng không
/// được vượt kế hoạch của ô. Ghi nhận theo từng ô, nhưng Xuất hàng chốt sổ CẢ NGÀY: mọi dây chuyền
/// của ngày đóng cùng lúc. Ô chỉ có sản lượng chính thức và phần thiếu sau khi Xuất hàng, và đóng
/// rồi là bất biến.
///
/// Thứ tự khóa thống nhất toàn hệ thống: Order → ProductionDay → ProductionPlan (CR-01 §5.6).
/// </summary>
public sealed class ProductionDayService(IAppDbContext db, IClock clock, ICurrentUser currentUser)
{
    public async Task<ProductionCellDetailDto> GetAsync(
        Guid orderId, DateOnly productionDate, Guid productionLineId, CancellationToken ct = default)
    {
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId, ct)
                    ?? throw new NotFoundException(ErrorCodes.OrderNotFound, "Order was not found.");

        var line = await FindLineAsync(productionLineId, ct);

        var plan = await db.ProductionPlans.AsNoTracking()
            .FirstOrDefaultAsync(p => p.OrderId == orderId
                                      && p.ProductionDate == productionDate
                                      && p.ProductionLineId == productionLineId, ct);

        var day = await db.ProductionDays.AsNoTracking()
            .FirstOrDefaultAsync(d => d.OrderId == orderId
                                      && d.ProductionDate == productionDate
                                      && d.ProductionLineId == productionLineId, ct);

        var entries = day is null ? [] : await LoadEntriesAsync(day.Id, ct);
        var totalActual = await GetTotalActualAsync(orderId, ct);

        return await BuildDetailAsync(order, line, productionDate, plan, day, entries, totalActual, ct);
    }

    public async Task<ProductionCellDetailDto> CreateEntryAsync(
        Guid orderId, DateOnly productionDate, Guid productionLineId,
        CreateProductionEntryRequest request, CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);

        var (order, plan, day) = await LockCellForWriteAsync(
            orderId, productionDate, productionLineId, createIfMissing: true, ct);

        // Đơn đã hoàn thành thì không ghi nhận thêm được nữa (CR-01 §14.6). Kiểm trước các ràng
        // buộc số lượng, vì đơn Completed luôn đã chạm trần và sẽ bị chặn bằng thông báo kém rõ hơn.
        GuardOrderNotCompleted(order);

        // Quantity được kiểm ở đây thay vì để entity ném, vì trần còn được nhập bên dưới cần một số
        // dương thì mới so sánh có nghĩa.
        if (request.Quantity <= 0)
        {
            throw new ValidationException(
                "quantity", "MUST_BE_GREATER_THAN_ZERO", "Quantity must be greater than zero.");
        }

        var cellActual = await GetCellActualAsync(day!.Id, ct);
        var totalActual = await GetTotalActualAsync(orderId, ct);

        GuardAllowance(request.Quantity, plan!.PlannedQuantity, cellActual, order.Quantity, totalActual);

        var now = clock.UtcNow;
        var entry = ProductionEntry.Create(day.Id, request.Quantity, request.Note, currentUser.UserId, now);

        db.ProductionEntries.Add(entry);
        db.ProductionEntryLogs.Add(ProductionEntryLog.Created(entry, currentUser.UserId, now));
        day.Touch(currentUser.UserId, now);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await GetAsync(orderId, productionDate, productionLineId, ct);
    }

    public async Task<ProductionCellDetailDto> UpdateEntryAsync(
        Guid entryId, UpdateProductionEntryRequest request, CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);

        var (order, plan, day, entry) = await LockEntryForWriteAsync(entryId, ct);

        GuardOrderNotCompleted(order);

        if (request.Quantity <= 0)
        {
            throw new ValidationException(
                "quantity", "MUST_BE_GREATER_THAN_ZERO", "Quantity must be greater than zero.");
        }

        var cellActual = await GetCellActualAsync(day.Id, ct);
        var totalActual = await GetTotalActualAsync(order.Id, ct);

        // NewCellActual = CellActual − OldQuantity + NewQuantity (CR-01 §6.5). Loại phần cũ ra khỏi
        // hai tổng rồi mới so trần, nên sửa xuống thấp hơn không bao giờ bị chặn nhầm.
        GuardAllowance(
            request.Quantity, plan.PlannedQuantity, cellActual - entry.Quantity,
            order.Quantity, totalActual - entry.Quantity);

        var now = clock.UtcNow;
        var oldQuantity = entry.Quantity;
        var oldNote = entry.Note;

        entry.Update(request.Quantity, request.Note, currentUser.UserId, now);
        db.ProductionEntryLogs.Add(ProductionEntryLog.Updated(
            entry.Id, oldQuantity, oldNote, entry.Quantity, entry.Note, currentUser.UserId, now));
        day.Touch(currentUser.UserId, now);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await GetAsync(order.Id, day.ProductionDate, day.ProductionLineId, ct);
    }

    public async Task<ProductionCellDetailDto> DeleteEntryAsync(Guid entryId, CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);

        var (order, _, day, entry) = await LockEntryForWriteAsync(entryId, ct);

        var now = clock.UtcNow;

        // Xóa mềm: lịch sử "đã nhập những gì" vẫn dựng lại được, và mọi phép SUM đều bỏ qua nhờ
        // global query filter (CR-01 §6.5, §14.9).
        db.ProductionEntryLogs.Add(ProductionEntryLog.Deleted(entry, currentUser.UserId, now));
        entry.Delete(currentUser.UserId, now);
        day.Touch(currentUser.UserId, now);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await GetAsync(order.Id, day.ProductionDate, day.ProductionLineId, ct);
    }

    /// <summary>
    /// Xuất hàng — chốt sổ CẢ NGÀY: mọi dây chuyền có kế hoạch mà còn mở trong ngày đó được đóng
    /// cùng lúc, trong một transaction — hoặc tất cả, hoặc không dây chuyền nào. Sản lượng chính
    /// thức của từng ô do server tính từ các lần ghi nhận; client không bao giờ gửi lên con số này
    /// (CR-01 §6.6, N-11).
    ///
    /// Ô đã đóng từ trước được bỏ qua chứ không làm hỏng cả lần xuất hàng. Ngày không còn ô nào mở
    /// thì đã xuất hàng rồi: 409, giống lần bấm thứ hai (CR-01 §14.7).
    /// </summary>
    public async Task<CloseProductionDayDto> CloseDayAsync(
        Guid orderId, DateOnly productionDate, CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);

        var order = await LockOrderForWriteAsync(orderId, productionDate, ct);

        // Chỉ ô có kế hoạch > 0 mới có gì để chốt (CR-001 BR-N10).
        var plans = await db.ProductionPlans
            .Where(p => p.OrderId == orderId && p.ProductionDate == productionDate && p.PlannedQuantity > 0)
            .ToListAsync(ct);

        if (plans.Count == 0)
        {
            throw new BusinessRuleException(
                ErrorCodes.DayHasNoPlan,
                "Nothing is planned on this day, so there is nothing to close.");
        }

        var days = await db.ProductionDays
            .Where(d => d.OrderId == orderId && d.ProductionDate == productionDate)
            .ToListAsync(ct);

        // Khóa theo cùng một thứ tự để hai request cùng ngày không khóa chéo nhau (CR-01 §5.6).
        foreach (var existing in days.OrderBy(d => d.Id))
        {
            await db.LockProductionDayAsync(existing.Id, ct);
        }

        var lineIds = plans.Select(p => p.ProductionLineId).ToList();
        var lines = await db.ProductionLines.AsNoTracking()
            .Where(l => lineIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, ct);

        var now = clock.UtcNow;
        var closedCells = new List<ClosedProductionCellDto>(plans.Count);

        // Cùng thứ tự với cột của ma trận: sortOrder rồi mã dây chuyền.
        foreach (var plan in plans
                     .OrderBy(p => lines[p.ProductionLineId].SortOrder)
                     .ThenBy(p => lines[p.ProductionLineId].Code))
        {
            var day = days.FirstOrDefault(d => d.ProductionLineId == plan.ProductionLineId);
            if (day?.IsClosed == true)
            {
                continue;
            }

            var actual = 0;
            if (day is null)
            {
                // Ô chưa ghi nhận lần nào: dòng production_days sinh ngay lúc chốt, sản lượng 0
                // (CR-01 §14.4 — Close-với-0).
                day = ProductionDay.Open(orderId, plan.ProductionLineId, productionDate, currentUser.UserId, now);
                db.ProductionDays.Add(day);
            }
            else
            {
                actual = await GetCellActualAsync(day.Id, ct);
            }

            day.Close(actual, currentUser.UserId, now);

            var line = lines[plan.ProductionLineId];
            closedCells.Add(new ClosedProductionCellDto(
                ProductionLineId: line.Id,
                ProductionLineCode: line.Code,
                ProductionLineName: line.Name,
                PlannedQuantity: plan.PlannedQuantity,
                ActualQuantity: actual,
                ShortageQuantity: Math.Max(plan.PlannedQuantity - actual, 0),
                Difference: actual - plan.PlannedQuantity));
        }

        if (closedCells.Count == 0)
        {
            throw new ConflictException(
                ErrorCodes.DayAlreadyClosed,
                "This production day has already been closed and can no longer be changed.");
        }

        // Đây là thời điểm DUY NHẤT trạng thái đơn hàng được đánh giá (CR-01 OV-4). Bước này bắt
        // buộc nằm trong cùng transaction với việc đóng ngày (CR-01 §4.6).
        var totalActual = await GetTotalActualAsync(orderId, ct);
        order.RecalculateStatus(totalActual, now);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var orderCompleted = order.Status == OrderStatus.Completed;

        return new CloseProductionDayDto(
            OrderId: orderId,
            ProductionDate: productionDate,
            ClosedAt: now,
            Cells: closedCells,
            OrderStatus: order.Status.ToString(),
            OrderCompleted: orderCompleted,
            // Đơn đã hoàn thành thì phần thiếu của các ô còn treo không cần xử lý nữa (CR-01 §14.6).
            HasShortage: !orderCompleted && closedCells.Any(c => c.ShortageQuantity > 0));
    }

    /// <summary>
    /// Khóa Order và kiểm các điều kiện chung của mọi thao tác ghi vào một ngày sản xuất của đơn.
    /// Khóa Order luôn đi trước mọi khóa khác (CR-01 §5.6).
    /// </summary>
    private async Task<Order> LockOrderForWriteAsync(Guid orderId, DateOnly productionDate, CancellationToken ct)
    {
        if (!await db.LockOrderAsync(orderId, ct))
        {
            throw new NotFoundException(ErrorCodes.OrderNotFound, "Order was not found.");
        }

        var order = await db.Orders.FirstAsync(o => o.Id == orderId, ct);

        // Đơn chưa lập tiến độ chưa có ô nào; nói thẳng lý do thay vì để nó rơi vào DAY_HAS_NO_PLAN.
        if (!order.IsScheduled)
        {
            throw new BusinessRuleException(
                ErrorCodes.OrderNotScheduled,
                "This order has no production schedule yet, so no production can be recorded for it.");
        }

        // Đơn hàng đã qua ngày kết thúc bị đóng băng — luật cũ, CR-001 không đụng tới.
        OrderMutationGuard.EnsureEditable(order, clock.Today);

        // Ngày tương lai chưa diễn ra nên không ghi nhận và không chốt sổ được (CR-01 N-05).
        if (productionDate > clock.Today)
        {
            throw new BusinessRuleException(
                ErrorCodes.FutureDateNotAllowed,
                "This production day has not happened yet.");
        }

        return order;
    }

    /// <summary>
    /// Khóa Order rồi ProductionDay, và kiểm mọi điều kiện chung của thao tác ghi lên một ô.
    /// Dòng <c>production_days</c> được tạo lazily, chỉ khi ô đó thực sự có kế hoạch (CR-01 §14.4).
    /// </summary>
    private async Task<(Order Order, ProductionPlan? Plan, ProductionDay? Day)> LockCellForWriteAsync(
        Guid orderId, DateOnly productionDate, Guid productionLineId, bool createIfMissing, CancellationToken ct)
    {
        var order = await LockOrderForWriteAsync(orderId, productionDate, ct);

        var plan = await db.ProductionPlans
            .FirstOrDefaultAsync(p => p.OrderId == orderId
                                      && p.ProductionDate == productionDate
                                      && p.ProductionLineId == productionLineId, ct);

        // "Không có kế hoạch" và "kế hoạch bằng 0" là cùng một câu trả lời nghiệp vụ: ô này không
        // sản xuất, nên không ghi nhận và không chốt sổ được (CR-01 §6.4 K-03, CR-001 BR-N10).
        if (plan is null || plan.PlannedQuantity == 0)
        {
            throw new BusinessRuleException(
                ErrorCodes.DayHasNoPlan,
                "This production line has nothing planned on this day, so nothing can be recorded or closed for it.");
        }

        var day = await db.ProductionDays
            .FirstOrDefaultAsync(d => d.OrderId == orderId
                                      && d.ProductionDate == productionDate
                                      && d.ProductionLineId == productionLineId, ct);

        day?.EnsureOpen();

        if (day is not null)
        {
            await db.LockProductionDayAsync(day.Id, ct);
        }
        else if (createIfMissing)
        {
            day = ProductionDay.Open(orderId, productionLineId, productionDate, currentUser.UserId, clock.UtcNow);
            db.ProductionDays.Add(day);

            // Dòng ô phải hiện hữu trước khi entry tham chiếu tới nó. Khóa Order đã tuần tự hóa các
            // request của cùng đơn hàng, nên không có race trên uq_production_days_order_date_line.
            await db.SaveChangesAsync(ct);
        }

        return (order, plan, day);
    }

    private async Task<(Order Order, ProductionPlan Plan, ProductionDay Day, ProductionEntry Entry)>
        LockEntryForWriteAsync(Guid entryId, CancellationToken ct)
    {
        var location = await db.ProductionEntries.AsNoTracking()
            .Where(e => e.Id == entryId)
            .Select(e => new { e.ProductionDay.OrderId, e.ProductionDay.ProductionDate, e.ProductionDay.ProductionLineId })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(
                ErrorCodes.ProductionEntryNotFound, "Production entry was not found.");

        var (order, plan, day) = await LockCellForWriteAsync(
            location.OrderId, location.ProductionDate, location.ProductionLineId, createIfMissing: false, ct);

        // Đọc lại sau khi đã khóa ô: một request khác có thể vừa xoá mềm chính entry này.
        var entry = await db.ProductionEntries.FirstOrDefaultAsync(e => e.Id == entryId, ct)
                    ?? throw new NotFoundException(
                        ErrorCodes.ProductionEntryNotFound, "Production entry was not found.");

        return (order, plan!, day!, entry);
    }

    /// <summary>
    /// Trần ghi nhận = MIN(kế hoạch của ô, số lượng đơn hàng). Ràng buộc nào chặt hơn thì ràng buộc
    /// đó thắng, và thông báo phải nói đúng con số còn được nhập (CR-01 §6.4).
    /// </summary>
    private static void GuardAllowance(
        int quantity, int plannedQuantity, int cellActualExcludingThis, int orderQuantity, int totalActualExcludingThis)
    {
        var cellAllowance = Math.Max(plannedQuantity - cellActualExcludingThis, 0);
        var orderAllowance = Math.Max(orderQuantity - totalActualExcludingThis, 0);

        if (quantity > cellAllowance && cellAllowance <= orderAllowance)
        {
            throw new BusinessRuleException(
                ErrorCodes.EntryExceedsDailyPlan,
                "The entry exceeds the remaining allowance for this production cell.",
                [new ValidationFailure("quantity", "MAX_ALLOWED", cellAllowance.ToString())]);
        }

        if (quantity > orderAllowance)
        {
            throw new BusinessRuleException(
                ErrorCodes.ActualExceedsOrderQuantity,
                "Total actual quantity cannot exceed the order quantity.",
                [new ValidationFailure("quantity", "MAX_ALLOWED", orderAllowance.ToString())]);
        }
    }

    private static void GuardOrderNotCompleted(Order order)
    {
        if (order.IsCompleted)
        {
            throw new ConflictException(
                ErrorCodes.OrderAlreadyCompleted,
                "This order is already completed, so no further production can be recorded for it.");
        }
    }

    private async Task<ProductionLine> FindLineAsync(Guid productionLineId, CancellationToken ct)
        => await db.ProductionLines.AsNoTracking().FirstOrDefaultAsync(l => l.Id == productionLineId, ct)
           ?? throw new NotFoundException(ErrorCodes.ProductionLineNotFound, "Production line was not found.");

    private async Task<int> GetCellActualAsync(Guid productionDayId, CancellationToken ct)
        => await db.ProductionEntries
            .Where(e => e.ProductionDayId == productionDayId)
            .SumAsync(e => (int?)e.Quantity, ct) ?? 0;

    /// <summary>
    /// Tổng thực tế toàn đơn trên MỌI ô, <b>bao gồm cả ô đang mở</b> — cố ý, để quản lý không nhập
    /// vượt tổng đơn ở ô cuối (CR-01 §4.5).
    /// </summary>
    private async Task<int> GetTotalActualAsync(Guid orderId, CancellationToken ct)
        => await db.ProductionEntries
            .Where(e => e.ProductionDay.OrderId == orderId)
            .SumAsync(e => (int?)e.Quantity, ct) ?? 0;

    private async Task<List<ProductionEntry>> LoadEntriesAsync(Guid productionDayId, CancellationToken ct)
        => await db.ProductionEntries.AsNoTracking()
            .Where(e => e.ProductionDayId == productionDayId)
            .OrderBy(e => e.RecordedAt)
            .ThenBy(e => e.Id)
            .ToListAsync(ct);

    private async Task<ProductionCellDetailDto> BuildDetailAsync(
        Order order,
        ProductionLine line,
        DateOnly productionDate,
        ProductionPlan? plan,
        ProductionDay? day,
        IReadOnlyList<ProductionEntry> entriesOldestFirst,
        int totalActual,
        CancellationToken ct)
    {
        var plannedQuantity = plan?.PlannedQuantity ?? 0;
        var cellActual = entriesOldestFirst.Sum(e => e.Quantity);
        var isClosed = day?.IsClosed == true;

        var userNames = await OrderQueries.UserDisplayNamesAsync(
            db,
            entriesOldestFirst.Select(e => e.CreatedBy).Concat(day?.ClosedBy is null ? [] : [day.ClosedBy.Value]),
            ct);

        var runningTotal = 0;
        var entries = new List<ProductionEntryDto>(entriesOldestFirst.Count);
        foreach (var entry in entriesOldestFirst)
        {
            runningTotal += entry.Quantity;
            entries.Add(new ProductionEntryDto(
                entry.Id, entry.Quantity, entry.RecordedAt, entry.Note, runningTotal, entry.IsEdited,
                userNames.GetValueOrDefault(entry.CreatedBy)));
        }

        // Mới nhất trên cùng (CR-01 §8.1), nhưng runningTotal đã được tính theo thứ tự thời gian.
        entries.Reverse();

        var cellAllowance = Math.Max(plannedQuantity - cellActual, 0);
        var orderAllowance = Math.Max(order.Quantity - totalActual, 0);

        // Ô đã đóng thì không còn được nhập gì nữa, bất kể hai trần bên trên còn chỗ.
        var remainingAllowance = isClosed ? 0 : Math.Min(cellAllowance, orderAllowance);

        return new ProductionCellDetailDto(
            OrderId: order.Id,
            ShoeCode: order.ShoeCode,
            ProductionDate: productionDate,
            ProductionLineId: line.Id,
            ProductionLineCode: line.Code,
            ProductionLineName: line.Name,
            DayStatus: ProductionDayQueries.DisplayStatusOf(
                plannedQuantity, productionDate, isClosed, clock.Today),
            InitialPlannedQuantity: plan?.InitialPlannedQuantity ?? 0,
            PlannedQuantity: plannedQuantity,
            AddOnQuantity: plan is null ? 0 : plan.PlannedQuantity - plan.InitialPlannedQuantity,
            DayActualQuantity: cellActual,
            IsProvisional: !isClosed,
            RemainingAllowance: remainingAllowance,
            RemainingAllowanceReason: cellAllowance <= orderAllowance
                ? RemainingAllowanceReason.DailyPlan
                : RemainingAllowanceReason.OrderQuantity,
            OrderRemainingQuantity: orderAllowance,
            OrderStatus: order.Status.ToString(),
            IsOrderReadOnly: order.IsPastDueDateOn(clock.Today),
            LastRecordedAt: entriesOldestFirst.Count == 0 ? null : entriesOldestFirst[^1].RecordedAt,
            ClosedAt: day?.ClosedAt,
            ClosedBy: day?.ClosedBy is null ? null : userNames.GetValueOrDefault(day.ClosedBy.Value),
            ShortageQuantity: ProductionCalculations.Shortage(plannedQuantity, day?.ActualQuantity),
            Difference: ProductionCalculations.Difference(plannedQuantity, day?.ActualQuantity),
            Entries: entries);
    }
}
