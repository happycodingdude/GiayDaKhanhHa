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

        // ORDER BY id cho mọi bên gọi cùng một thứ tự giành khóa, nhờ đó hai điều chỉnh đồng thời
        // đụng vào các ngày chồng nhau không bị deadlock (Step 4 §18).
        // Thứ tự này là của PostgreSQL chứ không phải của bên gọi: uuid được sắp theo byte trong
        // database, không khớp thứ tự so sánh Guid của .NET. Vì vậy sắp xếp mảng ở đây không chứng
        // minh được gì — ORDER BY trong câu lệnh bên dưới mới là thứ có tác dụng.
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
