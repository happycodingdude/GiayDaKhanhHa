using Microsoft.EntityFrameworkCore;
using ProductionManagement.Application.Abstractions;
using ProductionManagement.Application.Common;
using ProductionManagement.Application.Contracts;
using ProductionManagement.Domain;
using ProductionManagement.Domain.Entities;

namespace ProductionManagement.Application.Features.Orders;

/// <summary>
/// Lập tiến độ: chọn dây chuyền, khoảng thời gian và phân bổ hai tầng cho một đơn <c>Pending</c>
/// (CR-001 §6.6). Thao tác một lần cho mỗi đơn (BR-N05). Tiến độ vừa lập chưa chốt: còn sửa hoặc
/// xoá được, và chỉ sau khi chốt mới sản xuất được.
///
/// Phân chia trách nhiệm: mọi bất biến về CON SỐ nằm trong <see cref="Order"/>; service này lo phần
/// cần chạm database — dây chuyền có tồn tại không, có đang Active không, và khoá đơn để hai request
/// song song trên cùng một đơn không cùng chui lọt.
/// </summary>
public sealed class ProductionScheduleService(IAppDbContext db, IClock clock, OrderService orderService)
{
    public async Task<OrderDetailDto> CreateAsync(
        Guid orderId, CreateProductionScheduleRequest request, CancellationToken ct = default)
    {
        var lines = ValidateRequest(request);

        await using var transaction = await db.BeginTransactionAsync(ct);

        if (!await db.LockOrderAsync(orderId, ct))
        {
            throw new NotFoundException(ErrorCodes.OrderNotFound, "Order was not found.");
        }

        var order = await db.Orders.FirstAsync(o => o.Id == orderId, ct);

        // Đơn đã lập tiến độ thì 409 ngay, trước khi phí công kiểm dây chuyền (BR-N05).
        if (order.IsScheduled)
        {
            throw new ConflictException(
                ErrorCodes.OrderScheduleAlreadyExists, $"Order '{order.ShoeCode}' already has a production schedule.");
        }

        await GuardLinesSelectableAsync(lines, ct);

        order.Schedule(request.StartDate, request.DueDate, ToScheduleLines(lines), clock.UtcNow);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await orderService.GetByIdAsync(orderId, ct);
    }

    /// <summary>
    /// Sửa tiến độ chưa chốt. Request giống hệt lúc lập và được validate y như vậy; bộ mới thay toàn
    /// bộ bộ cũ.
    ///
    /// Không đi qua <see cref="OrderMutationGuard"/>: đóng băng sau ngày kết thúc là để giữ nguyên
    /// dữ liệu sản xuất, mà tiến độ chưa chốt thì chưa có dữ liệu sản xuất nào. Nhờ vậy đơn chưa chốt
    /// đã quá hạn vẫn dời ngày được. Tiến độ đã chốt thì <see cref="Order.Reschedule"/> tự từ chối.
    /// </summary>
    public async Task<OrderDetailDto> UpdateAsync(
        Guid orderId, CreateProductionScheduleRequest request, CancellationToken ct = default)
    {
        var lines = ValidateRequest(request);

        await using var transaction = await db.BeginTransactionAsync(ct);

        var order = await LockWithScheduleAsync(orderId, ct);

        await GuardLinesSelectableAsync(lines, ct);

        var (removedLines, removedPlans) = order.Reschedule(
            request.StartDate, request.DueDate, ToScheduleLines(lines), clock.UtcNow);

        // Quan hệ với đơn hàng là Restrict, nên dòng bị gỡ khỏi aggregate phải được xoá tường minh.
        db.OrderProductionLines.RemoveRange(removedLines);
        db.ProductionPlans.RemoveRange(removedPlans);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await orderService.GetByIdAsync(orderId, ct);
    }

    /// <summary>
    /// Xoá tiến độ chưa chốt: đơn quay về <c>Pending</c>, giữ nguyên mã giày, số lượng và ảnh để lập
    /// lại. Như <see cref="UpdateAsync"/>, không bị đóng băng sau ngày kết thúc.
    /// </summary>
    public async Task<OrderDetailDto> DeleteAsync(Guid orderId, CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);

        var order = await LockWithScheduleAsync(orderId, ct);

        var (removedLines, removedPlans) = order.Unschedule(clock.UtcNow);

        db.OrderProductionLines.RemoveRange(removedLines);
        db.ProductionPlans.RemoveRange(removedPlans);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await orderService.GetByIdAsync(orderId, ct);
    }

    /// <summary>Chốt tiến độ. Một chiều: từ đây đơn sản xuất được, và tiến độ không sửa được nữa.</summary>
    public async Task<OrderDetailDto> ConfirmAsync(Guid orderId, CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);

        if (!await db.LockOrderAsync(orderId, ct))
        {
            throw new NotFoundException(ErrorCodes.OrderNotFound, "Order was not found.");
        }

        var order = await db.Orders.FirstAsync(o => o.Id == orderId, ct);

        OrderMutationGuard.EnsureEditable(order, clock.Today);

        order.ConfirmSchedule(clock.UtcNow);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await orderService.GetByIdAsync(orderId, ct);
    }

    /// <summary>
    /// Khoá đơn rồi nạp kèm toàn bộ tiến độ, cho các thao tác thay hoặc gỡ tiến độ. Gọi bên trong
    /// transaction sẽ ghi.
    /// </summary>
    private async Task<Order> LockWithScheduleAsync(Guid orderId, CancellationToken ct)
    {
        if (!await db.LockOrderAsync(orderId, ct))
        {
            throw new NotFoundException(ErrorCodes.OrderNotFound, "Order was not found.");
        }

        return await db.Orders
            .Include(o => o.ProductionLines)
            .Include(o => o.ProductionPlans)
            .AsSplitQuery()
            .FirstAsync(o => o.Id == orderId, ct);
    }

    private static IReadOnlyList<ScheduleLineRequest> ValidateRequest(CreateProductionScheduleRequest request)
    {
        var lines = request.Lines ?? [];
        if (lines.Count == 0)
        {
            throw new ValidationException("lines", "REQUIRED", "At least one production line is required.");
        }

        // allocationMode chỉ để khai báo ý định; backend luôn validate con số thật (BR-N16).
        // Vẫn kiểm giá trị hợp lệ để một client gõ sai không lặng lẽ trôi qua.
        if (!string.IsNullOrWhiteSpace(request.AllocationMode)
            && !Enum.TryParse<AllocationMode>(request.AllocationMode, ignoreCase: true, out _))
        {
            throw new ValidationException(
                "allocationMode", "INVALID_VALUE", "Allocation mode must be 'Even' or 'Manual'.");
        }

        return lines;
    }

    private static List<ScheduleLine> ToScheduleLines(IReadOnlyList<ScheduleLineRequest> lines)
        => lines.Select(l => new ScheduleLine(
            l.ProductionLineId,
            l.AllocatedQuantity,
            (l.Plans ?? []).Select(p => (p.ProductionDate, p.PlannedQuantity)).ToList())).ToList();

    /// <summary>
    /// Dây chuyền phải tồn tại và đang <c>Active</c>. Dây chuyền đã ngừng hoạt động không được xuất
    /// hiện trong lựa chọn mới, dù dữ liệu cũ tham chiếu tới nó vẫn hiển thị bình thường (BR-N06,
    /// BR-N14).
    /// </summary>
    private async Task GuardLinesSelectableAsync(
        IReadOnlyList<ScheduleLineRequest> lines, CancellationToken ct)
    {
        var requestedIds = lines.Select(l => l.ProductionLineId).Distinct().ToList();

        var found = await db.ProductionLines.AsNoTracking()
            .Where(l => requestedIds.Contains(l.Id))
            .Select(l => new { l.Id, l.Code, l.Status })
            .ToListAsync(ct);

        var missing = requestedIds.Except(found.Select(l => l.Id)).ToList();
        if (missing.Count > 0)
        {
            throw new NotFoundException(
                ErrorCodes.ProductionLineNotFound,
                $"Production line(s) not found: {string.Join(", ", missing)}.");
        }

        var inactive = found.Where(l => l.Status != ProductionLineStatus.Active).ToList();
        if (inactive.Count > 0)
        {
            throw new BusinessRuleException(
                ErrorCodes.ProductionLineInactive,
                $"Production line(s) are inactive and cannot be scheduled: "
                + $"{string.Join(", ", inactive.Select(l => l.Code))}.",
                inactive.Select(l => new ValidationFailure(
                    l.Id.ToString(), ErrorCodes.ProductionLineInactive, l.Code)).ToList());
        }
    }
}
