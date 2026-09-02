using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductionManagement.Domain.Entities;

namespace ProductionManagement.Infrastructure.Persistence.Configurations;

public sealed class ProductionDayConfiguration : IEntityTypeConfiguration<ProductionDay>
{
    public void Configure(EntityTypeBuilder<ProductionDay> builder)
    {
        builder.ToTable("production_days", t =>
        {
            t.HasCheckConstraint("ck_production_days_actual_quantity", "actual_quantity >= 0");
            t.HasCheckConstraint("ck_production_days_status", "status IN ('Open', 'Closed')");

            // Ngày đã đóng luôn có đủ ảnh chụp sản lượng + dấu vết đóng; ngày mở thì không có gì cả.
            // Bất biến này được database giữ, không chỉ được giữ bởi code ứng dụng (CR-01 §5.1).
            t.HasCheckConstraint(
                "ck_production_days_closed_consistency",
                "(status = 'Closed' AND closed_at IS NOT NULL AND closed_by IS NOT NULL AND actual_quantity IS NOT NULL)"
                + " OR (status = 'Open' AND closed_at IS NULL AND closed_by IS NULL AND actual_quantity IS NULL)");
        });

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(d => d.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(d => d.ProductionDate).HasColumnName("production_date").HasColumnType("date").IsRequired();

        // varchar + CHECK thay vì enum gốc của PostgreSQL (Step 3 §5).
        builder.Property(d => d.Status)
            .HasColumnName("status").HasMaxLength(20).HasConversion<string>().IsRequired();

        builder.Property(d => d.ActualQuantity).HasColumnName("actual_quantity");
        builder.Property(d => d.ClosedAt).HasColumnName("closed_at");
        builder.Property(d => d.ClosedBy).HasColumnName("closed_by");
        builder.Property(d => d.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(d => d.UpdatedBy).HasColumnName("updated_by").IsRequired();
        builder.Property(d => d.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at").IsRequired();

        // Vẫn đúng một dòng cho mỗi đơn hàng mỗi ngày; điều đổi là ngày nay chứa N lần ghi nhận.
        builder.HasIndex(d => new { d.OrderId, d.ProductionDate })
            .IsUnique()
            .HasDatabaseName("uq_production_days_order_date");

        builder.HasIndex(d => new { d.Status, d.ProductionDate })
            .HasDatabaseName("ix_production_days_status_date");

        builder.HasOne(d => d.Order)
            .WithMany(o => o.ProductionDays)
            .HasForeignKey(d => d.OrderId)
            .HasConstraintName("fk_production_days_order")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(d => d.Entries)
            .WithOne(e => e.ProductionDay)
            .HasForeignKey(e => e.ProductionDayId)
            .HasConstraintName("fk_production_entries_day")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(d => d.CreatedBy)
            .HasConstraintName("fk_production_days_created_by")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(d => d.UpdatedBy)
            .HasConstraintName("fk_production_days_updated_by")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(d => d.ClosedBy)
            .HasConstraintName("fk_production_days_closed_by")
            .OnDelete(DeleteBehavior.Restrict);

        builder.Metadata.FindNavigation(nameof(ProductionDay.Entries))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
