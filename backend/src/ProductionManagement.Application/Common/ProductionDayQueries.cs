using Microsoft.EntityFrameworkCore;
using ProductionManagement.Application.Abstractions;
using ProductionManagement.Domain;

namespace ProductionManagement.Application.Common;

/// <summary>
/// Sản lượng của một ngày là tổng các lần ghi nhận chưa xoá, không phải một cột đọc thẳng ra được.
/// Gom cách tính đó về một chỗ để mọi màn hình (chi tiết đơn, thống kê, dashboard) không thể lệch
/// nhau — nhất là ở điểm dễ sai nhất: ngày còn mở có sản lượng tạm tính nhưng KHÔNG có phần thiếu
/// (CR-01 §4.5, §14.8).
/// </summary>
public sealed record ProductionDaySnapshot(
    Guid Id,
    Guid OrderId,
    DateOnly ProductionDate,
    ProductionDayStatus Status,
    int ActualQuantity,
    DateTimeOffset? ClosedAt,
    DateTimeOffset? LastRecordedAt,
    Guid? LastRecordedBy)
{
    public bool IsClosed => Status == ProductionDayStatus.Closed;

    /// <summary>Ảnh chụp sản lượng chính thức. Null khi ngày còn mở — phần thiếu bám theo giá trị này.</summary>
    public int? ClosedActualQuantity => IsClosed ? ActualQuantity : null;
}

public static class ProductionDayQueries
{
    /// <summary>
    /// Trạng thái hiển thị của một ngày, suy ra chứ không lưu — một khoản bù làm kế hoạch từ 0 thành
    /// 40 sẽ khiến trạng thái lưu cứng lệch ngay (CR-01 §4.3, §14.3).
    /// Thứ tự kiểm tra quan trọng: NoPlan xét trước NotStarted.
    /// </summary>
    public static ProductionDayDisplayStatus DisplayStatusOf(
        int plannedQuantity, DateOnly productionDate, bool isClosed, DateOnly today)
    {
        if (plannedQuantity == 0)
        {
            return ProductionDayDisplayStatus.NoPlan;
        }

        if (productionDate > today)
        {
            return ProductionDayDisplayStatus.NotStarted;
        }

        return isClosed ? ProductionDayDisplayStatus.Closed : ProductionDayDisplayStatus.InProduction;
    }

    public static Task<List<ProductionDaySnapshot>> SnapshotsForOrderAsync(
        this IAppDbContext db, Guid orderId, CancellationToken ct = default)
        => db.SnapshotQuery(d => d.OrderId == orderId).ToListAsync(ct);

    public static Task<List<ProductionDaySnapshot>> SnapshotsForOrdersAsync(
        this IAppDbContext db, IReadOnlyCollection<Guid> orderIds, CancellationToken ct = default)
        => orderIds.Count == 0
            ? Task.FromResult(new List<ProductionDaySnapshot>())
            : db.SnapshotQuery(d => orderIds.Contains(d.OrderId)).ToListAsync(ct);

    public static Task<List<ProductionDaySnapshot>> AllSnapshotsAsync(
        this IAppDbContext db, CancellationToken ct = default)
        => db.SnapshotQuery(_ => true).ToListAsync(ct);

    private static IQueryable<ProductionDaySnapshot> SnapshotQuery(
        this IAppDbContext db, System.Linq.Expressions.Expression<Func<Domain.Entities.ProductionDay, bool>> filter)
        => db.ProductionDays.AsNoTracking()
            .Where(filter)
            .Select(d => new ProductionDaySnapshot(
                d.Id,
                d.OrderId,
                d.ProductionDate,
                d.Status,
                // Ngày đã đóng dùng ảnh chụp, ngày còn mở cộng sống các lần ghi nhận. Hai giá trị
                // này bằng nhau với ngày đã đóng, nhưng đọc ảnh chụp là rẻ hơn và đúng theo định
                // nghĩa "bất biến sau khi đóng".
                d.ActualQuantity ?? d.Entries.Sum(e => (int?)e.Quantity) ?? 0,
                d.ClosedAt,
                d.Entries.OrderByDescending(e => e.RecordedAt).Select(e => (DateTimeOffset?)e.RecordedAt).FirstOrDefault(),
                d.Entries.OrderByDescending(e => e.RecordedAt).Select(e => (Guid?)e.CreatedBy).FirstOrDefault()));
}
