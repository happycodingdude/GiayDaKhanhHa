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
            t.HasCheckConstraint("ck_orders_status", "status IN ('Pending', 'Incomplete', 'Completed')");

            // Ngày thuộc về tiến độ: đơn chưa lập tiến độ không có ngày nào, đơn đã lập thì có đủ cả
            // hai và đúng thứ tự. Bất biến này do database giữ, không chỉ do code (CR-001 §5.2, BR-N04).
            t.HasCheckConstraint(
                "ck_orders_schedule_dates",
                "(status = 'Pending' AND start_date IS NULL AND due_date IS NULL)"
                + " OR (status <> 'Pending' AND start_date IS NOT NULL AND due_date IS NOT NULL"
                + " AND start_date <= due_date)");

            // Bốn cột ảnh đi cùng nhau: có tất cả, hoặc không có gì.
            t.HasCheckConstraint(
                "ck_orders_image",
                "(image_path IS NULL AND image_file_name IS NULL"
                + " AND image_content_type IS NULL AND image_size_bytes IS NULL)"
                + " OR (image_path IS NOT NULL AND image_file_name IS NOT NULL"
                + " AND image_content_type IS NOT NULL AND image_size_bytes IS NOT NULL"
                + " AND image_size_bytes > 0)");
        });

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(o => o.ShoeCode).HasColumnName("shoe_code").HasMaxLength(50).IsRequired();
        builder.Property(o => o.Quantity).HasColumnName("quantity").IsRequired();

        // DB chỉ giữ đường dẫn tương đối; file nằm trên disk của server và chỉ ra ngoài qua endpoint
        // có xác thực (CR-001 §2 QĐ-6).
        builder.Property(o => o.ImagePath).HasColumnName("image_path").HasMaxLength(500);
        builder.Property(o => o.ImageFileName).HasColumnName("image_file_name").HasMaxLength(255);
        builder.Property(o => o.ImageContentType).HasColumnName("image_content_type").HasMaxLength(100);
        builder.Property(o => o.ImageSizeBytes).HasColumnName("image_size_bytes");

        // Ngày nghiệp vụ là giá trị chỉ có ngày, không gắn múi giờ (Step 3 §8).
        builder.Property(o => o.StartDate).HasColumnName("start_date").HasColumnType("date");
        builder.Property(o => o.DueDate).HasColumnName("due_date").HasColumnType("date");

        builder.Property(o => o.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(o => o.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(o => o.ShoeCode).IsUnique().HasDatabaseName("uq_orders_shoe_code");
        builder.HasIndex(o => o.Status).HasDatabaseName("ix_orders_status");

        builder.Metadata.FindNavigation(nameof(Order.ProductionPlans))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(Order.ProductionDays))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(Order.ProductionLines))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
