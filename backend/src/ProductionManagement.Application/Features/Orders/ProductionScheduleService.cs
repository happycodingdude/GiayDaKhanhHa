using Microsoft.EntityFrameworkCore;
using ProductionManagement.Application.Abstractions;
using ProductionManagement.Application.Contracts;
using ProductionManagement.Domain;
using ProductionManagement.Domain.Entities;

namespace ProductionManagement.Application.Features.Orders;

/// <summary>
/// Lập tiến độ: chốt dây chuyền, khoảng thời gian và phân bổ hai tầng cho một đơn <c>Pending</c>
/// (CR-001 §6.6). Thao tác một lần cho mỗi đơn (BR-N05).
///
/// Phân chia trách nhiệm: mọi bất biến về CON SỐ nằm trong <see cref="Order.Schedule"/>; service này
/// lo phần cần chạm database — dây chuyền có tồn tại không, có đang Active không, và khoá đơn để
/// hai request lập tiến độ song song không cùng chui lọt.
/// </summary>
public sealed class ProductionScheduleService(IAppDbContext db, IClock clock, OrderService orderService)
{
    public async Task<OrderDetailDto> CreateAsync(
        Guid orderId, CreateProductionScheduleRequest request, CancellationToken ct = default)
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

        var now = clock.UtcNow;

        order.Schedule(
            request.StartDate,
            request.DueDate,
            lines.Select(l => new ScheduleLine(
                l.ProductionLineId,
                l.AllocatedQuantity,
                (l.Plans ?? []).Select(p => (p.ProductionDate, p.PlannedQuantity)).ToList())).ToList(),
            now);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await orderService.GetByIdAsync(orderId, ct);
    }

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
