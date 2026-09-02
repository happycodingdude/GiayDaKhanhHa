using Microsoft.EntityFrameworkCore;
using ProductionManagement.Application.Abstractions;
using ProductionManagement.Application.Common;
using ProductionManagement.Application.Contracts;
using ProductionManagement.Domain;
using ProductionManagement.Domain.Entities;
using ProductionManagement.Domain.Services;

namespace ProductionManagement.Application.Features.Production;

/// <summary>
/// Ghi nhận sản lượng nhiều lần trong ngày và chốt sổ ngày sản xuất (Xuất hàng) — CR-01.
///
/// Sản lượng thực tế là số cộng thêm: một ngày có N lần ghi nhận, tổng không được vượt kế hoạch
/// ngày. Ngày chỉ có sản lượng chính thức và phần thiếu sau khi Xuất hàng, và đóng rồi là bất biến.
///
/// Thứ tự khóa thống nhất toàn hệ thống: Order → ProductionDay → ProductionPlan (CR-01 §5.6).
/// </summary>
public sealed class ProductionDayService(IAppDbContext db, IClock clock, ICurrentUser currentUser)
{
    public async Task<ProductionDayDetailDto> GetAsync(
        Guid orderId, DateOnly productionDate, CancellationToken ct = default)
    {
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId, ct)
                    ?? throw new NotFoundException(ErrorCodes.OrderNotFound, "Order was not found.");

        var plan = await db.ProductionPlans.AsNoTracking()
            .FirstOrDefaultAsync(p => p.OrderId == orderId && p.ProductionDate == productionDate, ct);

        var day = await db.ProductionDays.AsNoTracking()
            .FirstOrDefaultAsync(d => d.OrderId == orderId && d.ProductionDate == productionDate, ct);

        var entries = day is null ? [] : await LoadEntriesAsync(day.Id, ct);
        var totalActual = await GetTotalActualAsync(orderId, ct);

        return await BuildDetailAsync(order, productionDate, plan, day, entries, totalActual, ct);
    }

    public async Task<ProductionDayDetailDto> CreateEntryAsync(
        Guid orderId, DateOnly productionDate, CreateProductionEntryRequest request, CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);

        var (order, plan, day) = await LockDayForWriteAsync(orderId, productionDate, createIfMissing: true, ct);

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

        var dayActual = await GetDayActualAsync(day!.Id, ct);
        var totalActual = await GetTotalActualAsync(orderId, ct);

        GuardAllowance(request.Quantity, plan!.PlannedQuantity, dayActual, order.Quantity, totalActual);

        var now = clock.UtcNow;
        var entry = ProductionEntry.Create(day.Id, request.Quantity, request.Note, currentUser.UserId, now);

        db.ProductionEntries.Add(entry);
        db.ProductionEntryLogs.Add(ProductionEntryLog.Created(entry, currentUser.UserId, now));
        day.Touch(currentUser.UserId, now);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await GetAsync(orderId, productionDate, ct);
    }

    public async Task<ProductionDayDetailDto> UpdateEntryAsync(
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

        var dayActual = await GetDayActualAsync(day.Id, ct);
        var totalActual = await GetTotalActualAsync(order.Id, ct);

        // NewDayActual = DayActual − OldQuantity + NewQuantity (CR-01 §6.5). Loại phần cũ ra khỏi
        // hai tổng rồi mới so trần, nên sửa xuống thấp hơn không bao giờ bị chặn nhầm.
        GuardAllowance(
            request.Quantity, plan.PlannedQuantity, dayActual - entry.Quantity,
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

        return await GetAsync(order.Id, day.ProductionDate, ct);
    }

    public async Task<ProductionDayDetailDto> DeleteEntryAsync(Guid entryId, CancellationToken ct = default)
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

        return await GetAsync(order.Id, day.ProductionDate, ct);
    }

    /// <summary>
    /// Xuất hàng — chốt sổ ngày sản xuất. Sản lượng chính thức do server tính từ các lần ghi nhận;
    /// client không bao giờ gửi lên <c>actualQuantity</c> (CR-01 §6.6, N-11).
    /// </summary>
    public async Task<CloseProductionDayDto> CloseAsync(
        Guid orderId, DateOnly productionDate, CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);

        var (order, plan, day) = await LockDayForWriteAsync(orderId, productionDate, createIfMissing: true, ct);

        var now = clock.UtcNow;
        var actual = await GetDayActualAsync(day!.Id, ct);

        day.Close(actual, currentUser.UserId, now);

        // Đây là thời điểm DUY NHẤT trạng thái đơn hàng được đánh giá (CR-01 OV-4). Bước này bắt
        // buộc nằm trong cùng transaction với việc đóng ngày (CR-01 §4.6).
        var totalActual = await GetTotalActualAsync(orderId, ct);
        var orderEntity = await db.Orders.FirstAsync(o => o.Id == orderId, ct);
        orderEntity.RecalculateStatus(totalActual, now);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var shortage = Math.Max(plan!.PlannedQuantity - actual, 0);

        return new CloseProductionDayDto(
            OrderId: orderId,
            ProductionDate: productionDate,
            DayStatus: ProductionDayDisplayStatus.Closed,
            PlannedQuantity: plan.PlannedQuantity,
            ActualQuantity: actual,
            ShortageQuantity: shortage,
            Difference: actual - plan.PlannedQuantity,
            ClosedAt: now,
            OrderStatus: orderEntity.Status.ToString(),
            OrderCompleted: orderEntity.Status == OrderStatus.Completed,
            // Đơn đã hoàn thành thì phần thiếu của các ngày còn treo không cần xử lý nữa (CR-01 §14.6).
            HasShortage: shortage > 0 && orderEntity.Status != OrderStatus.Completed);
    }

    /// <summary>
    /// Khóa Order rồi ProductionDay, và kiểm mọi điều kiện chung của thao tác ghi lên một ngày.
    /// Dòng <c>production_days</c> được tạo lazily, chỉ khi ngày đó thực sự có kế hoạch (CR-01 §14.4).
    /// </summary>
    private async Task<(Order Order, ProductionPlan? Plan, ProductionDay? Day)> LockDayForWriteAsync(
        Guid orderId, DateOnly productionDate, bool createIfMissing, CancellationToken ct)
    {
        if (!await db.LockOrderAsync(orderId, ct))
        {
            throw new NotFoundException(ErrorCodes.OrderNotFound, "Order was not found.");
        }

        var order = await db.Orders.FirstAsync(o => o.Id == orderId, ct);

        // Đơn hàng đã qua ngày hạn bị đóng băng — luật cũ, CR-01 không đụng tới.
        OrderMutationGuard.EnsureEditable(order, clock.Today);

        // Ngày tương lai chưa diễn ra nên không ghi nhận và không chốt sổ được (CR-01 N-05).
        if (productionDate > clock.Today)
        {
            throw new BusinessRuleException(
                ErrorCodes.FutureDateNotAllowed,
                "This production day has not happened yet.");
        }

        var plan = await db.ProductionPlans
            .FirstOrDefaultAsync(p => p.OrderId == orderId && p.ProductionDate == productionDate, ct);

        // "Không có kế hoạch" và "kế hoạch bằng 0" là cùng một câu trả lời nghiệp vụ: ngày này
        // không sản xuất, nên không ghi nhận và không chốt sổ được (CR-01 §6.4, K-03).
        if (plan is null || plan.PlannedQuantity == 0)
        {
            throw new BusinessRuleException(
                ErrorCodes.DayHasNoPlan,
                "This day has no production planned, so nothing can be recorded or closed for it.");
        }

        var day = await db.ProductionDays
            .FirstOrDefaultAsync(d => d.OrderId == orderId && d.ProductionDate == productionDate, ct);

        day?.EnsureOpen();

        if (day is not null)
        {
            await db.LockProductionDayAsync(day.Id, ct);
        }
        else if (createIfMissing)
        {
            day = ProductionDay.Open(orderId, productionDate, currentUser.UserId, clock.UtcNow);
            db.ProductionDays.Add(day);

            // Dòng ngày phải hiện hữu trước khi entry tham chiếu tới nó. Khóa Order đã tuần tự hóa
            // các request của cùng đơn hàng, nên không có race trên uq_production_days_order_date.
            await db.SaveChangesAsync(ct);
        }

        return (order, plan, day);
    }

    private async Task<(Order Order, ProductionPlan Plan, ProductionDay Day, ProductionEntry Entry)>
        LockEntryForWriteAsync(Guid entryId, CancellationToken ct)
    {
        var location = await db.ProductionEntries.AsNoTracking()
            .Where(e => e.Id == entryId)
            .Select(e => new { e.ProductionDay.OrderId, e.ProductionDay.ProductionDate })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(
                ErrorCodes.ProductionEntryNotFound, "Production entry was not found.");

        var (order, plan, day) = await LockDayForWriteAsync(
            location.OrderId, location.ProductionDate, createIfMissing: false, ct);

        // Đọc lại sau khi đã khóa ngày: một request khác có thể vừa xoá mềm chính entry này.
        var entry = await db.ProductionEntries.FirstOrDefaultAsync(e => e.Id == entryId, ct)
                    ?? throw new NotFoundException(
                        ErrorCodes.ProductionEntryNotFound, "Production entry was not found.");

        return (order, plan!, day!, entry);
    }

    /// <summary>
    /// Trần ghi nhận = MIN(kế hoạch ngày, số lượng đơn hàng). Ràng buộc nào chặt hơn thì ràng buộc
    /// đó thắng, và thông báo phải nói đúng con số còn được nhập (CR-01 §6.4).
    /// </summary>
    private static void GuardAllowance(
        int quantity, int plannedQuantity, int dayActualExcludingThis, int orderQuantity, int totalActualExcludingThis)
    {
        var dayAllowance = Math.Max(plannedQuantity - dayActualExcludingThis, 0);
        var orderAllowance = Math.Max(orderQuantity - totalActualExcludingThis, 0);

        if (quantity > dayAllowance && dayAllowance <= orderAllowance)
        {
            throw new BusinessRuleException(
                ErrorCodes.EntryExceedsDailyPlan,
                "The entry exceeds the remaining allowance for this production day.",
                [new ValidationFailure("quantity", "MAX_ALLOWED", dayAllowance.ToString())]);
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

    private async Task<int> GetDayActualAsync(Guid productionDayId, CancellationToken ct)
        => await db.ProductionEntries
            .Where(e => e.ProductionDayId == productionDayId)
            .SumAsync(e => (int?)e.Quantity, ct) ?? 0;

    /// <summary>
    /// Tổng thực tế toàn đơn, <b>bao gồm cả ngày đang mở</b> — cố ý, để quản lý không nhập vượt
    /// tổng đơn trong ngày cuối (CR-01 §4.5).
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

    private async Task<ProductionDayDetailDto> BuildDetailAsync(
        Order order,
        DateOnly productionDate,
        ProductionPlan? plan,
        ProductionDay? day,
        IReadOnlyList<ProductionEntry> entriesOldestFirst,
        int totalActual,
        CancellationToken ct)
    {
        var plannedQuantity = plan?.PlannedQuantity ?? 0;
        var dayActual = entriesOldestFirst.Sum(e => e.Quantity);
        var isClosed = day?.IsClosed == true;

        var userNames = await GetUserDisplayNamesAsync(
            entriesOldestFirst.Select(e => e.CreatedBy).Concat(day?.ClosedBy is null ? [] : [day.ClosedBy.Value]), ct);

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

        var dayAllowance = Math.Max(plannedQuantity - dayActual, 0);
        var orderAllowance = Math.Max(order.Quantity - totalActual, 0);

        // Ngày đã đóng thì không còn được nhập gì nữa, bất kể hai trần bên trên còn chỗ.
        var remainingAllowance = isClosed ? 0 : Math.Min(dayAllowance, orderAllowance);

        return new ProductionDayDetailDto(
            OrderId: order.Id,
            OrderCode: order.OrderCode,
            ProductionDate: productionDate,
            DayStatus: ProductionDayQueries.DisplayStatusOf(
                plannedQuantity, productionDate, day?.IsClosed == true, clock.Today),
            InitialPlannedQuantity: plan?.InitialPlannedQuantity ?? 0,
            PlannedQuantity: plannedQuantity,
            AddOnQuantity: plan is null ? 0 : plan.PlannedQuantity - plan.InitialPlannedQuantity,
            DayActualQuantity: dayActual,
            IsProvisional: !isClosed,
            RemainingAllowance: remainingAllowance,
            RemainingAllowanceReason: dayAllowance <= orderAllowance
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
}
