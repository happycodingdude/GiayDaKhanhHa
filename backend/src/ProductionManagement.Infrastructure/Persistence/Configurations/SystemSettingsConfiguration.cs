using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductionManagement.Domain.Entities;

namespace ProductionManagement.Infrastructure.Persistence.Configurations;

public sealed class SystemSettingsConfiguration : IEntityTypeConfiguration<SystemSettings>
{
    public void Configure(EntityTypeBuilder<SystemSettings> builder)
    {
        builder.ToTable("system_settings", t =>
            t.HasCheckConstraint(
                "ck_system_settings_interval", "recording_interval_minutes BETWEEN 5 AND 480"));

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(s => s.RecordingIntervalMinutes).HasColumnName("recording_interval_minutes").IsRequired();
        builder.Property(s => s.RemindBeforeDue).HasColumnName("remind_before_due").IsRequired();
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by").IsRequired();
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(s => s.UpdatedBy)
            .HasConstraintName("fk_system_settings_updated_by")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
