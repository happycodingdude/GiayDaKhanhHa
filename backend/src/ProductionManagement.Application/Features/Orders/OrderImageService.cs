using Microsoft.EntityFrameworkCore;
using ProductionManagement.Application.Abstractions;
using ProductionManagement.Application.Common;
using ProductionManagement.Application.Contracts;
using ProductionManagement.Domain;
using ProductionManagement.Domain.Entities;

namespace ProductionManagement.Application.Features.Orders;

/// <summary>Nội dung ảnh để stream ra response. Đường dẫn vật lý không nằm trong đây.</summary>
public sealed record OrderImageContent(Stream Content, string ContentType, string FileName);

/// <summary>
/// Ảnh mẫu của đơn hàng: mỗi đơn tối đa một ảnh, ảnh mới thay ảnh cũ (CR-001 BR-N02).
///
/// Thứ tự ghi ở cả hai thao tác đều là: ghi file mới → commit database → mới xoá file cũ. Xoá file
/// trước khi commit thì một lần rollback sẽ để lại dòng database trỏ tới file đã biến mất.
/// </summary>
public sealed class OrderImageService(IAppDbContext db, IClock clock, IOrderImageStorage storage)
{
    public async Task<OrderDetailDto> ReplaceAsync(
        Guid orderId, ValidatedImage image, CancellationToken ct = default)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct)
                    ?? throw new NotFoundException(ErrorCodes.OrderNotFound, "Order was not found.");

        OrderMutationGuard.EnsureEditable(order, clock.Today);

        using var content = new MemoryStream(image.Content, writable: false);
        var newPath = await storage.SaveAsync(orderId, image.Extension, content, ct);

        string? previousPath;
        try
        {
            previousPath = order.AttachImage(
                new OrderImage(newPath, image.FileName, image.ContentType, image.Content.Length), clock.UtcNow);

            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // Database chưa đổi thì file mới là rác — dọn ngay thay vì để lại cho job dọn dẹp.
            storage.Delete(newPath);
            throw;
        }

        if (previousPath is not null)
        {
            storage.Delete(previousPath);
        }

        return await BuildDetailAsync(order, ct);
    }

    public async Task<OrderDetailDto> DeleteAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct)
                    ?? throw new NotFoundException(ErrorCodes.OrderNotFound, "Order was not found.");

        OrderMutationGuard.EnsureEditable(order, clock.Today);

        var previousPath = order.RemoveImage(clock.UtcNow);
        await db.SaveChangesAsync(ct);

        storage.Delete(previousPath);

        return await BuildDetailAsync(order, ct);
    }

    /// <summary>
    /// Nội dung ảnh, phục vụ qua endpoint có xác thực. Client không bao giờ nhìn thấy đường dẫn vật
    /// lý (CR-001 §2 QĐ-6, §6.4).
    /// </summary>
    public async Task<OrderImageContent> GetContentAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await db.Orders.AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => new { o.ImagePath, o.ImageContentType, o.ImageFileName })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(ErrorCodes.OrderNotFound, "Order was not found.");

        if (order.ImagePath is null)
        {
            throw new NotFoundException(ErrorCodes.ImageNotFound, "This order has no image.");
        }

        // File biến mất khỏi disk mà dòng database vẫn còn: trả 404 chứ không phải 500 — đây là dữ
        // liệu thiếu, không phải lỗi lập trình.
        var content = storage.Open(order.ImagePath)
            ?? throw new NotFoundException(ErrorCodes.ImageNotFound, "The image file is no longer available.");

        return new OrderImageContent(content, order.ImageContentType!, order.ImageFileName!);
    }

    private async Task<OrderDetailDto> BuildDetailAsync(Order order, CancellationToken ct)
    {
        var cells = (await OrderQueries.PlanCellsAsync(db, [order.Id], ct))[order.Id].ToList();
        var snapshots = await db.SnapshotsForOrderAsync(order.Id, ct);
        var lines = (await OrderQueries.ProductionLinesAsync(db, [order.Id], ct))[order.Id].ToList();

        var derived = OrderDerivedCalculator.Compute(
            order.Quantity, order.Status, order.DueDate,
            cells, snapshots.Select(d => d.ToActualCell()).ToList(), clock.Today);

        return OrderService.ToDetailDto(order, lines, derived);
    }
}
