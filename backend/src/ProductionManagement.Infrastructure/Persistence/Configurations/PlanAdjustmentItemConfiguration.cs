using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductionManagement.Domain.Entities;

namespace ProductionManagement.Infrastructure.Persistence.Configurations;

public sealed class PlanAdjustmentItemConfiguration : IEntityTypeConfiguration<PlanAdjustmentItem>
{
    public void Configure(EntityTypeBuilder<PlanAdjustmentItem> builder)
    {
        builder.ToTable("plan_adjustment_items", t =>
            t.HasCheckConstraint("ck_plan_adjustment_items_add_on", "add_on_quantity > 0"));

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(i => i.PlanAdjustmentId).HasColumnName("plan_adjustment_id").IsRequired();
        builder.Property(i => i.ProductionPlanId).HasColumnName("production_plan_id").IsRequired();
        builder.Property(i => i.AddOnQuantity).HasColumnName("add_on_quantity").IsRequired();

        // The same target plan cannot appear twice in one adjustment.
        builder.HasIndex(i => new { i.PlanAdjustmentId, i.ProductionPlanId })
            .IsUnique()
            .HasDatabaseName("uq_plan_adjustment_items_adjustment_plan");

        builder.HasIndex(i => i.PlanAdjustmentId).HasDatabaseName("ix_plan_adjustment_items_adjustment");
        builder.HasIndex(i => i.ProductionPlanId).HasDatabaseName("ix_plan_adjustment_items_target_plan");

        builder.HasOne(i => i.PlanAdjustment)
            .WithMany(a => a.Items)
            .HasForeignKey(i => i.PlanAdjustmentId)
            .HasConstraintName("fk_plan_adjustment_items_adjustment")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.ProductionPlan)
            .WithMany()
            .HasForeignKey(i => i.ProductionPlanId)
            .HasConstraintName("fk_plan_adjustment_items_target_plan")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
