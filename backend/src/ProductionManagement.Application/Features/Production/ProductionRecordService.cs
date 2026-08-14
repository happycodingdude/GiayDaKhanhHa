using Microsoft.EntityFrameworkCore;
using ProductionManagement.Application.Abstractions;
using ProductionManagement.Application.Common;
using ProductionManagement.Application.Contracts;
using ProductionManagement.Application.Features.Adjustments;
using ProductionManagement.Domain;
using ProductionManagement.Domain.Entities;

namespace ProductionManagement.Application.Features.Production;

/// <summary>
/// Tạo/sửa sản lượng thực tế theo ngày. Thực tế là một giá trị, không phải số cộng thêm — chủ đích
/// không có thao tác cộng dồn nào (Step 4 §7, §21).
/// </summary>
public sealed class ProductionRecordService(
    IAppDbContext db,
    IClock clock,
    ICurrentUser currentUser,
    ActiveAdjustmentRecalculator adjustmentRecalculator)
{
    public async Task<ProductionRecordDto> CreateAsync(
        Guid orderId, CreateProductionRecordRequest request, CancellationToken ct = default)
    {
        if (request.ActualQuantity < 0)
        {
            throw new ValidationException(
                "actualQuantity", "MUST_BE_GREATER_THAN_OR_EQUAL_TO_ZERO", "Actual quantity cannot be negative.");
        }

        await using var transaction = await db.BeginTransactionAsync(ct);

        // Khóa đơn hàng trước để hai request đồng thời không thể cùng đọc một tổng đã cũ rồi độc lập
        // vượt qua kiểm tra SUM(actual) <= Order.Quantity (Step 3 §10).
        if (!await db.LockOrderAsync(orderId, ct))
        {
            throw new NotFoundException(ErrorCodes.OrderNotFound, "Order was not found.");
        }

        var order = await db.Orders.FirstAsync(o => o.Id == orderId, ct);

        // Đơn hàng quá hạn là chỉ đọc, nên phần thiếu lúc kết thúc được giữ nguyên hệt như vậy.
        OrderMutationGuard.EnsureEditable(order, clock.Today);

        var plan = await db.ProductionPlans
            .FirstOrDefaultAsync(p => p.OrderId == orderId && p.ProductionDate == request.ProductionDate, ct);

        if (plan is null)
        {
            throw new BusinessRuleException(
                ErrorCodes.NoProductionPlanForDate,
                "There is no production plan for this date, so an actual quantity cannot be recorded.");
        }

        // Ngày có kế hoạch bằng 0 thì không nhập được thực tế — kể cả số 0 nhập tường minh
        // (master summary §6, actual entry spec §4.1).
        if (plan.PlannedQuantity == 0)
        {
            throw new BusinessRuleException(
                ErrorCodes.PlanQuantityIsZero,
                "This day has no production planned. Adjust the plan before recording an actual quantity.");
        }

        // Thực tế là số đã sản xuất nên không thể ghi trước khi ngày đó diễn ra. Hôm nay thì được:
        // thực tế nhập vào cuối ngày. Kiểm tra sau phần kế hoạch để "ngày này không có trong kế
        // hoạch" vẫn là câu trả lời cụ thể hơn.
        if (request.ProductionDate > clock.Today)
        {
            throw new BusinessRuleException(
                ErrorCodes.FutureProductionDate,
                "This production day has not happened yet, so an actual quantity cannot be recorded for it.");
        }

        var existing = await db.ProductionRecords
            .FirstOrDefaultAsync(r => r.OrderId == orderId && r.ProductionDate == request.ProductionDate, ct);

        if (existing is not null)
        {
            throw new ConflictException(
                ErrorCodes.ProductionRecordAlreadyExists,
                "An actual quantity has already been recorded for this date. Edit the existing record instead.");
        }

        var currentTotal = await db.ProductionRecords
            .Where(r => r.OrderId == orderId)
            .SumAsync(r => (int?)r.ActualQuantity, ct) ?? 0;

        var newTotal = currentTotal + request.ActualQuantity;
        GuardTotalActual(newTotal, order.Quantity, currentTotal);

        var now = clock.UtcNow;
        var record = ProductionRecord.Create(orderId, request.ProductionDate, request.ActualQuantity, currentUser.UserId, now);
        db.ProductionRecords.Add(record);

        order.RecalculateStatus(newTotal, now);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            await transaction.RollbackAsync(ct);
            throw new ConflictException(
                ErrorCodes.ProductionRecordAlreadyExists,
                "An actual quantity has already been recorded for this date. Edit the existing record instead.");
        }

        await transaction.CommitAsync(ct);

        return ToDto(record);
    }

    public async Task<ProductionRecordDto> UpdateAsync(
        Guid orderId, Guid productionRecordId, UpdateProductionRecordRequest request, CancellationToken ct = default)
    {
        if (request.ActualQuantity < 0)
        {
            throw new ValidationException(
                "actualQuantity", "MUST_BE_GREATER_THAN_OR_EQUAL_TO_ZERO", "Actual quantity cannot be negative.");
        }

        await using var transaction = await db.BeginTransactionAsync(ct);

        if (!await db.LockOrderAsync(orderId, ct))
        {
            throw new NotFoundException(ErrorCodes.OrderNotFound, "Order was not found.");
        }

        var order = await db.Orders.FirstAsync(o => o.Id == orderId, ct);

        OrderMutationGuard.EnsureEditable(order, clock.Today);

        var record = await db.ProductionRecords
            .FirstOrDefaultAsync(r => r.Id == productionRecordId && r.OrderId == orderId, ct)
            ?? throw new NotFoundException(ErrorCodes.ProductionRecordNotFound, "Production record was not found.");

        var currentTotal = await db.ProductionRecords
            .Where(r => r.OrderId == orderId)
            .SumAsync(r => (int?)r.ActualQuantity, ct) ?? 0;

        // NewTotal = CurrentTotal - OldActual + NewActual (Step 4 §7).
        var newTotal = currentTotal - record.ActualQuantity + request.ActualQuantity;
        GuardTotalActual(newTotal, order.Quantity, currentTotal - record.ActualQuantity);

        var now = clock.UtcNow;
        record.UpdateActual(request.ActualQuantity, currentUser.UserId, now);

        // Trạng thái bám theo tổng ở cả hai chiều: một lần sửa làm tổng tụt xuống dưới số lượng đơn
        // hàng sẽ đưa đơn Completed trở lại Incomplete (Step 1 §13).
        order.RecalculateStatus(newTotal, now);

        // Lưu trước khi tính lại, vì bước tính lại đọc ngược sản lượng thực tế mới từ database.
        await db.SaveChangesAsync(ct);

        // Phần thiếu mà ngày này đã được điều chỉnh vừa thay đổi, nên khoản bù dựa trên nó được dựng
        // lại từ phần thiếu mới. Vẫn nằm trong cùng transaction, nên thực tế và điều chỉnh của nó
        // không bao giờ mâu thuẫn nhau.
        var recalculation = await adjustmentRecalculator.RecalculateAsync(orderId, record.ProductionDate, ct);

        await transaction.CommitAsync(ct);

        return ToDto(record, recalculation);
    }

    private static void GuardTotalActual(int newTotal, int orderQuantity, int totalExcludingThisDay)
    {
        if (newTotal <= orderQuantity)
        {
            return;
        }

        var maximum = Math.Max(orderQuantity - totalExcludingThisDay, 0);
        throw new BusinessRuleException(
            ErrorCodes.ActualExceedsOrderQuantity,
            $"Total actual quantity cannot exceed the order quantity. At most {maximum} can be recorded for this day.");
    }

    private static ProductionRecordDto ToDto(
        ProductionRecord record, AdjustmentRecalculationDto? recalculation = null)
        => new(
            record.Id, record.OrderId, record.ProductionDate, record.ActualQuantity,
            record.CreatedAt, record.UpdatedAt, recalculation);

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException?.GetType().GetProperty("SqlState")?.GetValue(ex.InnerException) as string == "23505";
}
