using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ProductionManagement.Application.Abstractions;
using ProductionManagement.Domain.Entities;

namespace ProductionManagement.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<ProductionPlan> ProductionPlans => Set<ProductionPlan>();
    public DbSet<ProductionRecord> ProductionRecords => Set<ProductionRecord>();
    public DbSet<PlanAdjustment> PlanAdjustments => Set<PlanAdjustment>();
    public DbSet<PlanAdjustmentItem> PlanAdjustmentItems => Set<PlanAdjustmentItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => await Database.BeginTransactionAsync(cancellationToken);

    public async Task<bool> LockOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var locked = await Database
            .SqlQuery<Guid>($"SELECT id AS \"Value\" FROM orders WHERE id = {orderId} FOR UPDATE")
            .ToListAsync(cancellationToken);

        return locked.Count > 0;
    }

    public async Task LockProductionPlansAsync(
        IReadOnlyCollection<Guid> productionPlanIds, CancellationToken cancellationToken = default)
    {
        if (productionPlanIds.Count == 0)
        {
            return;
        }

        var ids = productionPlanIds.Distinct().ToArray();

        // ORDER BY id gives every caller the same lock acquisition order, which keeps two
        // concurrent adjustments touching overlapping days from deadlocking (Step 4 §18).
        // The ordering is PostgreSQL's, not the caller's: uuid sorts byte-wise in the database,
        // which does not match Guid's comparison order in .NET. Sorting the array here would
        // therefore prove nothing — the ORDER BY in the statement below is what matters.
        await Database
            .SqlQuery<Guid>($"SELECT id AS \"Value\" FROM production_plans WHERE id = ANY({ids}) ORDER BY id FOR UPDATE")
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> LockPlanAdjustmentAsync(Guid planAdjustmentId, CancellationToken cancellationToken = default)
    {
        var locked = await Database
            .SqlQuery<Guid>($"SELECT id AS \"Value\" FROM plan_adjustments WHERE id = {planAdjustmentId} FOR UPDATE")
            .ToListAsync(cancellationToken);

        return locked.Count > 0;
    }
}
