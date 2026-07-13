using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotifyHub.Domain.Entities;

namespace NotifyHub.Infrastructure.Persistence.Configurations;

public class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        builder.ToTable("system_settings");

        builder.HasKey(s => s.Key);
        builder.Property(s => s.Key).HasMaxLength(100);

        builder.Property(s => s.Value).HasMaxLength(200).IsRequired();
    }
}
