using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ProductionManagement.Domain.Entities;

namespace ProductionManagement.Application.Abstractions;

/// <summary>
/// Bề mặt persistence mà các use case của application dùng. Row lock được để lộ tường minh vì các
/// bất biến liên dòng được bảo vệ bằng transaction + row lock chứ không phải bằng trigger database
/// hay cột version (Step 4 §18).
/// </summary>
public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Order> Orders { get; }
    DbSet<ProductionPlan> ProductionPlans { get; }
    DbSet<ProductionDay> ProductionDays { get; }
    DbSet<ProductionEntry> ProductionEntries { get; }
    DbSet<ProductionEntryLog> ProductionEntryLogs { get; }
    DbSet<SystemSettings> SystemSettings { get; }
    DbSet<PlanAdjustment> PlanAdjustments { get; }
    DbSet<PlanAdjustmentItem> PlanAdjustmentItems { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>Khóa dòng đơn hàng (SELECT ... FOR UPDATE). Trả về false khi không tồn tại.</summary>
    Task<bool> LockOrderAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Khóa dòng ngày sản xuất. Khóa đơn hàng là chưa đủ cho bất biến "tổng ghi nhận trong ngày
    /// &lt;= kế hoạch ngày" (CR-01 §5.6). Thứ tự khóa thống nhất toàn hệ thống:
    /// Order → ProductionDay → ProductionPlan.
    /// </summary>
    Task LockProductionDayAsync(Guid productionDayId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Khóa dòng các kế hoạch sản xuất được chỉ định. Các dòng được khóa theo thứ tự id tăng dần để
    /// giảm rủi ro deadlock (Step 4 §18).
    /// </summary>
    Task LockProductionPlansAsync(IReadOnlyCollection<Guid> productionPlanIds, CancellationToken cancellationToken = default);

    /// <summary>Khóa dòng bản ghi điều chỉnh kế hoạch. Trả về false khi không tồn tại.</summary>
    Task<bool> LockPlanAdjustmentAsync(Guid planAdjustmentId, CancellationToken cancellationToken = default);
}
