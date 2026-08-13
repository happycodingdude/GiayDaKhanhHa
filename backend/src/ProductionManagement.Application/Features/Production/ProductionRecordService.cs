using Microsoft.EntityFrameworkCore;
using ProductionManagement.Application.Abstractions;
using ProductionManagement.Application.Contracts;
using ProductionManagement.Application.Features.Adjustments;
using ProductionManagement.Domain;
using ProductionManagement.Domain.Entities;

namespace ProductionManagement.Application.Features.Production;

/// <summary>
/// Create/edit of the daily actual. Actual is a value, not an increment — there is deliberately no
/// add/increment operation (Step 4 §7, §21).
/// </summary>
public sealed class ProductionRecordService(
    IAppDbContext db,
    IClock clock,
    ICurrentUser currentUser,
    ActiveAdjustmentRecalculator adjustmentRecalculator)
{
    public async Task<ProductionRecordDto> CreateAsync(
        long orderId, CreateProductionRecordRequest request, CancellationToken ct = default)
    {
        if (request.ActualQuantity < 0)
        {
            throw new ValidationException(
                "actualQuantity", "MUST_BE_GREATER_THAN_OR_EQUAL_TO_ZERO", "Actual quantity cannot be negative.");
        }

        await using var transaction = await db.BeginTransactionAsync(ct);

        // Lock the order first so two concurrent requests cannot each read a stale total and
        // independently pass the SUM(actual) <= Order.Quantity check (Step 3 §10).
        if (!await db.LockOrderAsync(orderId, ct))
        {
            throw new NotFoundException(ErrorCodes.OrderNotFound, "Order was not found.");
        }

        var order = await db.Orders.FirstAsync(o => o.Id == orderId, ct);

        var plan = await db.ProductionPlans
            .FirstOrDefaultAsync(p => p.OrderId == orderId && p.ProductionDate == request.ProductionDate, ct);

        if (plan is null)
        {
            throw new BusinessRuleException(
                ErrorCodes.NoProductionPlanForDate,
                "There is no production plan for this date, so an actual quantity cannot be recorded.");
        }

        // A day planned for 0 cannot receive an actual at all — not even an explicit 0
        // (master summary §6, actual entry spec §4.1).
        if (plan.PlannedQuantity == 0)
        {
            throw new BusinessRuleException(
                ErrorCodes.PlanQuantityIsZero,
                "This day has no production planned. Adjust the plan before recording an actual quantity.");
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
        long orderId, long productionRecordId, UpdateProductionRecordRequest request, CancellationToken ct = default)
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

        // The status follows the total in both directions: an edit that drops the total below the
        // order quantity moves a Completed order back to Incomplete (Step 1 §13).
        order.RecalculateStatus(newTotal, now);

        // Saved before the recalculation because that reads the new actual back from the database.
        await db.SaveChangesAsync(ct);

        // The shortage this day was adjusted for has just changed, so the add-on that was based on
        // it is rebuilt from the new shortage. Still inside the same transaction, so the actual and
        // its adjustment can never disagree.
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
