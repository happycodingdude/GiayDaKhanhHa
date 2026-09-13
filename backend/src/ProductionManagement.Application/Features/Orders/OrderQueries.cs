using Microsoft.EntityFrameworkCore;
using ProductionManagement.Application.Abstractions;
using ProductionManagement.Application.Common;
using ProductionManagement.Application.Contracts;
using ProductionManagement.Domain.Entities;

namespace ProductionManagement.Application.Features.Orders;

/// <summary>
/// Những mảnh đọc dữ liệu mà nhiều service dùng chung (đơn hàng, tiến độ, ảnh, thống kê). Gom về một
/// chỗ để URL ảnh và cách dựng danh sách dây chuyền không bị mỗi nơi viết một kiểu.
/// </summary>
public static class OrderQueries
{
    /// <summary>
    /// Ảnh chỉ ra ngoài qua endpoint có xác thực; đường dẫn vật lý không bao giờ lộ (CR-001 §2 QĐ-6).
    /// </summary>
    public static string? ImageUrlFor(Order order)
        => order.HasImage ? $"/api/v1/orders/{order.Id}/image/content" : null;

    /// <summary>Các ô kế hoạch của một tập đơn hàng, dạng đã sẵn sàng cho OrderDerivedCalculator.</summary>
    public static async Task<ILookup<Guid, PlanCell>> PlanCellsAsync(
        IAppDbContext db, IReadOnlyCollection<Guid> orderIds, CancellationToken ct)
    {
        if (orderIds.Count == 0)
        {
            return Enumerable.Empty<(Guid, PlanCell)>().ToLookup(x => x.Item1, x => x.Item2);
        }

        var rows = await db.ProductionPlans.AsNoTracking()
            .Where(p => orderIds.Contains(p.OrderId))
            .Select(p => new
            {
                p.OrderId,
                p.ProductionDate,
                p.ProductionLineId,
                p.PlannedQuantity,
                p.InitialPlannedQuantity,
            })
            .ToListAsync(ct);

        return rows.ToLookup(
            r => r.OrderId,
            r => new PlanCell(r.ProductionDate, r.ProductionLineId, r.PlannedQuantity, r.InitialPlannedQuantity));
    }

    public static ActualCell ToActualCell(this ProductionDaySnapshot snapshot)
        => new(snapshot.ProductionDate, snapshot.ProductionLineId, snapshot.ActualQuantity, snapshot.IsClosed);

    /// <summary>
    /// Dây chuyền thuộc tiến độ của một tập đơn hàng, sắp theo thứ tự chia đều đã chốt:
    /// <c>sort_order</c> rồi <c>code</c> (CR-001 §6.6b).
    /// </summary>
    public static async Task<ILookup<Guid, OrderProductionLineDto>> ProductionLinesAsync(
        IAppDbContext db, IReadOnlyCollection<Guid> orderIds, CancellationToken ct)
    {
        if (orderIds.Count == 0)
        {
            return Enumerable.Empty<(Guid, OrderProductionLineDto)>().ToLookup(x => x.Item1, x => x.Item2);
        }

        var rows = await db.OrderProductionLines.AsNoTracking()
            .Where(l => orderIds.Contains(l.OrderId))
            .OrderBy(l => l.ProductionLine.SortOrder)
            .ThenBy(l => l.ProductionLine.Code)
            .Select(l => new
            {
                l.OrderId,
                Dto = new OrderProductionLineDto(
                    l.ProductionLineId,
                    l.ProductionLine.Code,
                    l.ProductionLine.Name,
                    l.ProductionLine.Status.ToString(),
                    l.ProductionLine.SortOrder,
                    l.AllocatedQuantity),
            })
            .ToListAsync(ct);

        return rows.ToLookup(r => r.OrderId, r => r.Dto);
    }

    public static async Task<Dictionary<Guid, string>> UserDisplayNamesAsync(
        IAppDbContext db, IEnumerable<Guid> userIds, CancellationToken ct)
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
