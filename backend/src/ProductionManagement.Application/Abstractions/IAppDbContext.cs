using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ProductionManagement.Domain.Entities;

namespace ProductionManagement.Application.Abstractions;

/// <summary>
/// Persistence surface used by the application use cases. Row locking is exposed explicitly
/// because the cross-row invariants are protected by transaction + row locking rather than by
/// database triggers or a version column (Step 4 §18).
/// </summary>
public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Order> Orders { get; }
    DbSet<ProductionPlan> ProductionPlans { get; }
    DbSet<ProductionRecord> ProductionRecords { get; }
    DbSet<PlanAdjustment> PlanAdjustments { get; }
    DbSet<PlanAdjustmentItem> PlanAdjustmentItems { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>Takes a row lock on the order (SELECT ... FOR UPDATE). Returns false when it does not exist.</summary>
    Task<bool> LockOrderAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes row locks on the given production plans. Rows are locked in ascending id order to
    /// reduce deadlock risk (Step 4 §18).
    /// </summary>
    Task LockProductionPlansAsync(IReadOnlyCollection<Guid> productionPlanIds, CancellationToken cancellationToken = default);

    /// <summary>Takes a row lock on the plan adjustment. Returns false when it does not exist.</summary>
    Task<bool> LockPlanAdjustmentAsync(Guid planAdjustmentId, CancellationToken cancellationToken = default);
}
