using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductionManagement.Domain.Entities;

namespace ProductionManagement.Infrastructure.Persistence.Configurations;

public sealed class ProductionPlanConfiguration : IEntityTypeConfiguration<ProductionPlan>
{
    public void Configure(EntityTypeBuilder<ProductionPlan> builder)
    {
        builder.ToTable("production_plans", t =>
        {
            t.HasCheckConstraint("ck_production_plans_initial_quantity", "initial_planned_quantity >= 0");
            t.HasCheckConstraint("ck_production_plans_quantity", "planned_quantity >= 0");
        });

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(p => p.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(p => p.ProductionDate).HasColumnName("production_date").HasColumnType("date").IsRequired();
        builder.Property(p => p.InitialPlannedQuantity).HasColumnName("initial_planned_quantity").IsRequired();
        builder.Property(p => p.PlannedQuantity).HasColumnName("planned_quantity").IsRequired();
        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at").IsRequired();

        // Mỗi đơn hàng mỗi ngày chỉ một kế hoạch sản xuất.
        builder.HasIndex(p => new { p.OrderId, p.ProductionDate })
            .IsUnique()
            .HasDatabaseName("uq_production_plans_order_date");

        // Lịch sử sản xuất không bao giờ được biến mất vì xóa dây chuyền (Step 3 §7).
        builder.HasOne(p => p.Order)
            .WithMany(o => o.ProductionPlans)
            .HasForeignKey(p => p.OrderId)
            .HasConstraintName("fk_production_plans_order")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
