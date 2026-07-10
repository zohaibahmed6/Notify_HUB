using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotifyHub.Domain.Entities;

namespace NotifyHub.Infrastructure.Persistence.Configurations;

public class DeliveryStatusHistoryConfiguration : IEntityTypeConfiguration<DeliveryStatusHistory>
{
    public void Configure(EntityTypeBuilder<DeliveryStatusHistory> builder)
    {
        builder.ToTable("delivery_status_history");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(h => h.OccurredAt).IsRequired();

        builder.HasOne(h => h.Message)
            .WithMany(m => m.StatusHistory)
            .HasForeignKey(h => h.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(h => h.MessageId);
    }
}
