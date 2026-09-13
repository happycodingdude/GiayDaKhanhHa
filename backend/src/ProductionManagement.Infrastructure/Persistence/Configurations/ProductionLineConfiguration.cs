using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductionManagement.Domain.Entities;

namespace ProductionManagement.Infrastructure.Persistence.Configurations;

public sealed class ProductionLineConfiguration : IEntityTypeConfiguration<ProductionLine>
{
    public void Configure(EntityTypeBuilder<ProductionLine> builder)
    {
        builder.ToTable("production_lines", t =>
        {
            t.HasCheckConstraint("ck_production_lines_status", "status IN ('Active', 'Inactive')");
            t.HasCheckConstraint("ck_production_lines_sort_order", "sort_order >= 0");
        });

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(l => l.Code).HasColumnName("code").HasMaxLength(30).IsRequired();
        builder.Property(l => l.Name).HasColumnName("name").HasMaxLength(100).IsRequired();

        // varchar + CHECK thay cho enum gốc của PostgreSQL (Step 3 §5).
        builder.Property(l => l.Status)
            .HasColumnName("status").HasMaxLength(20).HasConversion<string>().IsRequired();

        builder.Property(l => l.SortOrder).HasColumnName("sort_order").HasDefaultValue(0).IsRequired();
        builder.Property(l => l.Note).HasColumnName("note").HasMaxLength(500);
        builder.Property(l => l.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(l => l.Code).IsUnique().HasDatabaseName("uq_production_lines_code");
    }
}
