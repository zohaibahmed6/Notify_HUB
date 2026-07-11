using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotifyHub.Domain.Entities;

namespace NotifyHub.Infrastructure.Persistence.Configurations;

public class InboundMessageConfiguration : IEntityTypeConfiguration<InboundMessage>
{
    public void Configure(EntityTypeBuilder<InboundMessage> builder)
    {
        builder.ToTable("inbound_messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Body).HasMaxLength(1000).IsRequired();
        builder.Property(m => m.ReceivedAt).IsRequired();

        builder.HasOne(m => m.Thread)
            .WithMany(t => t.InboundMessages)
            .HasForeignKey(m => m.ThreadId)
            .OnDelete(DeleteBehavior.Cascade);

        // §7: required for paginated/indexed inbox (FR-010).
        builder.HasIndex(m => new { m.ThreadId, m.ReceivedAt });
    }
}
