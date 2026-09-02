using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductionManagement.Domain.Entities;

namespace ProductionManagement.Infrastructure.Persistence.Configurations;

public sealed class ProductionEntryLogConfiguration : IEntityTypeConfiguration<ProductionEntryLog>
{
    public void Configure(EntityTypeBuilder<ProductionEntryLog> builder)
    {
        builder.ToTable("production_entry_logs", t =>
            t.HasCheckConstraint(
                "ck_production_entry_logs_action", "action IN ('Create', 'Update', 'Delete')"));

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(l => l.ProductionEntryId).HasColumnName("production_entry_id").IsRequired();
        builder.Property(l => l.Action)
            .HasColumnName("action").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(l => l.OldQuantity).HasColumnName("old_quantity");
        builder.Property(l => l.NewQuantity).HasColumnName("new_quantity");
        builder.Property(l => l.OldNote).HasColumnName("old_note").HasMaxLength(255);
        builder.Property(l => l.NewNote).HasColumnName("new_note").HasMaxLength(255);
        builder.Property(l => l.ChangedBy).HasColumnName("changed_by").IsRequired();
        builder.Property(l => l.ChangedAt).HasColumnName("changed_at").IsRequired();

        builder.HasIndex(l => new { l.ProductionEntryId, l.ChangedAt })
            .HasDatabaseName("ix_production_entry_logs_entry");

        // Vết thay đổi phải sống lâu hơn lần ghi nhận bị xoá mềm, nên quan hệ này chủ đích không
        // đi kèm query filter của ProductionEntry.
        builder.HasOne<ProductionEntry>()
            .WithMany()
            .HasForeignKey(l => l.ProductionEntryId)
            .HasConstraintName("fk_production_entry_logs_entry")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(l => l.ChangedBy)
            .HasConstraintName("fk_production_entry_logs_changed_by")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
