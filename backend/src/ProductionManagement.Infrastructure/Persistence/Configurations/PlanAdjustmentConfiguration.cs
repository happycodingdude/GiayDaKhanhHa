using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductionManagement.Domain.Entities;

namespace ProductionManagement.Infrastructure.Persistence.Configurations;

public sealed class PlanAdjustmentConfiguration : IEntityTypeConfiguration<PlanAdjustment>
{
    public void Configure(EntityTypeBuilder<PlanAdjustment> builder)
    {
        builder.ToTable("plan_adjustments", t =>
        {
            t.HasCheckConstraint("ck_plan_adjustments_shortage", "shortage_quantity > 0");
            t.HasCheckConstraint("ck_plan_adjustments_type", "adjustment_type IN ('Manual', 'Automatic')");
            t.HasCheckConstraint("ck_plan_adjustments_status", "status IN ('Applied', 'Reversed')");
        });

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id").UseIdentityAlwaysColumn();

        // There is intentionally no order_id: the Order is reached through the source plan (Step 3 §4.5).
        builder.Property(a => a.SourceProductionPlanId).HasColumnName("source_production_plan_id").IsRequired();
        builder.Property(a => a.ShortageQuantity).HasColumnName("shortage_quantity").IsRequired();

        builder.Property(a => a.AdjustmentType)
            .HasColumnName("adjustment_type")
            .HasMaxLength(20)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(a => a.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(a => a.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(a => a.AppliedBy).HasColumnName("applied_by");
        builder.Property(a => a.ReversedBy).HasColumnName("reversed_by");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(a => a.AppliedAt).HasColumnName("applied_at");
        builder.Property(a => a.ReversedAt).HasColumnName("reversed_at");

        builder.HasIndex(a => a.SourceProductionPlanId).HasDatabaseName("ix_plan_adjustments_source_plan");

        builder.HasOne(a => a.SourceProductionPlan)
            .WithMany()
            .HasForeignKey(a => a.SourceProductionPlanId)
            .HasConstraintName("fk_plan_adjustments_source_plan")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.CreatedBy)
            .HasConstraintName("fk_plan_adjustments_created_by")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.AppliedBy)
            .HasConstraintName("fk_plan_adjustments_applied_by")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.ReversedBy)
            .HasConstraintName("fk_plan_adjustments_reversed_by")
            .OnDelete(DeleteBehavior.Restrict);

        builder.Metadata.FindNavigation(nameof(PlanAdjustment.Items))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
