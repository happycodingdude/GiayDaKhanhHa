using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductionManagement.Domain.Entities;

namespace ProductionManagement.Infrastructure.Persistence.Configurations;

public sealed class ProductionRecordConfiguration : IEntityTypeConfiguration<ProductionRecord>
{
    public void Configure(EntityTypeBuilder<ProductionRecord> builder)
    {
        builder.ToTable("production_records", t =>
            t.HasCheckConstraint("ck_production_records_actual_quantity", "actual_quantity >= 0"));

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(r => r.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(r => r.ProductionDate).HasColumnName("production_date").HasColumnType("date").IsRequired();
        builder.Property(r => r.ActualQuantity).HasColumnName("actual_quantity").IsRequired();
        builder.Property(r => r.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by").IsRequired();
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at").IsRequired();

        // Mỗi đơn hàng mỗi ngày một bản ghi thực tế. Đây cũng là thứ ngăn việc nhập trùng trong ngày
        // mà không cần thêm bảng idempotency (Step 4 §17).
        builder.HasIndex(r => new { r.OrderId, r.ProductionDate })
            .IsUnique()
            .HasDatabaseName("uq_production_records_order_date");

        builder.HasOne(r => r.Order)
            .WithMany(o => o.ProductionRecords)
            .HasForeignKey(r => r.OrderId)
            .HasConstraintName("fk_production_records_order")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.CreatedBy)
            .HasConstraintName("fk_production_records_created_by")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.UpdatedBy)
            .HasConstraintName("fk_production_records_updated_by")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
