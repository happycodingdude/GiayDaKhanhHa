using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductionManagement.Domain.Entities;

namespace ProductionManagement.Infrastructure.Persistence.Configurations;

public sealed class OrderProductionLineConfiguration : IEntityTypeConfiguration<OrderProductionLine>
{
    public void Configure(EntityTypeBuilder<OrderProductionLine> builder)
    {
        builder.ToTable("order_production_lines", t =>
            t.HasCheckConstraint("ck_order_production_lines_allocated", "allocated_quantity > 0"));

        // Khoá chính là cặp (đơn hàng, dây chuyền): một dây chuyền chỉ được gán một lần cho một đơn.
        builder.HasKey(l => new { l.OrderId, l.ProductionLineId })
            .HasName("pk_order_production_lines");

        builder.Property(l => l.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(l => l.ProductionLineId).HasColumnName("production_line_id").IsRequired();
        builder.Property(l => l.AllocatedQuantity).HasColumnName("allocated_quantity").IsRequired();
        builder.Property(l => l.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(l => l.ProductionLineId).HasDatabaseName("ix_order_production_lines_line");

        builder.HasOne(l => l.Order)
            .WithMany(o => o.ProductionLines)
            .HasForeignKey(l => l.OrderId)
            .HasConstraintName("fk_order_production_lines_order")
            .OnDelete(DeleteBehavior.Restrict);

        // Lịch sử sản xuất không bao giờ biến mất vì dây chuyền bị gỡ khỏi danh mục (BR-N15).
        builder.HasOne(l => l.ProductionLine)
            .WithMany()
            .HasForeignKey(l => l.ProductionLineId)
            .HasConstraintName("fk_order_production_lines_line")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
