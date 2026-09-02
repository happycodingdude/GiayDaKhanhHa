using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductionManagement.Domain.Entities;

namespace ProductionManagement.Infrastructure.Persistence.Configurations;

public sealed class ProductionEntryConfiguration : IEntityTypeConfiguration<ProductionEntry>
{
    public void Configure(EntityTypeBuilder<ProductionEntry> builder)
    {
        builder.ToTable("production_entries", t =>
            t.HasCheckConstraint("ck_production_entries_quantity_positive", "quantity > 0"));

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(e => e.ProductionDayId).HasColumnName("production_day_id").IsRequired();
        builder.Property(e => e.Quantity).HasColumnName("quantity").IsRequired();
        builder.Property(e => e.RecordedAt).HasColumnName("recorded_at").IsRequired();
        builder.Property(e => e.Note).HasColumnName("note").HasMaxLength(255);
        builder.Property(e => e.DeletedAt).HasColumnName("deleted_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(e => new { e.ProductionDayId, e.RecordedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_production_entries_day_recorded_at");

        builder.HasIndex(e => e.ProductionDayId)
            .HasFilter("deleted_at IS NULL")
            .HasDatabaseName("ix_production_entries_day_active");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.CreatedBy)
            .HasConstraintName("fk_production_entries_created_by")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.UpdatedBy)
            .HasConstraintName("fk_production_entries_updated_by")
            .OnDelete(DeleteBehavior.Restrict);

        // Quên filter này là mọi phép SUM sẽ cộng cả lần ghi nhận đã xoá (CR-01 §14.9). Truy vấn
        // lịch sử đầy đủ phải gọi IgnoreQueryFilters() một cách tường minh.
        builder.HasQueryFilter(e => e.DeletedAt == null);
    }
}
