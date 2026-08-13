using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductionManagement.Domain.Entities;

namespace ProductionManagement.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders", t =>
        {
            t.HasCheckConstraint("ck_orders_quantity_positive", "quantity > 0");
            t.HasCheckConstraint("ck_orders_date_range", "start_date <= due_date");
            t.HasCheckConstraint("ck_orders_status", "status IN ('Incomplete', 'Completed')");
        });

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasColumnName("id").UseIdentityAlwaysColumn();

        builder.Property(o => o.OrderCode).HasColumnName("order_code").HasMaxLength(50).IsRequired();
        builder.Property(o => o.Quantity).HasColumnName("quantity").IsRequired();

        // Business dates are date-only with no timezone attached (Step 3 §8).
        builder.Property(o => o.StartDate).HasColumnName("start_date").HasColumnType("date").IsRequired();
        builder.Property(o => o.DueDate).HasColumnName("due_date").HasColumnType("date").IsRequired();

        builder.Property(o => o.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(o => o.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(o => o.OrderCode).IsUnique().HasDatabaseName("uq_orders_order_code");

        builder.Metadata.FindNavigation(nameof(Order.ProductionPlans))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(Order.ProductionRecords))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
