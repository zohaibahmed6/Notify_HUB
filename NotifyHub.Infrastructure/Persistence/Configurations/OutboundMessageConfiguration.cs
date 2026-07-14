using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotifyHub.Domain.Entities;

namespace NotifyHub.Infrastructure.Persistence.Configurations;

public class OutboundMessageConfiguration : IEntityTypeConfiguration<OutboundMessage>
{
    public void Configure(EntityTypeBuilder<OutboundMessage> builder)
    {
        builder.ToTable("outbound_messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.SenderType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(m => m.TriggerReference).HasMaxLength(200);

        builder.Property(m => m.SentByUsername).HasMaxLength(100);

        builder.Property(m => m.RenderedBody).HasMaxLength(1000);

        builder.Property(m => m.CreatedAt).IsRequired();

        builder.Property(m => m.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(m => m.IdempotencyKey).HasMaxLength(64);
        builder.HasIndex(m => m.IdempotencyKey).IsUnique();

        builder.Property(m => m.AttemptCount).IsRequired();

        builder.HasOne(m => m.Patient)
            .WithMany()
            .HasForeignKey(m => m.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Template)
            .WithMany(t => t.OutboundMessages)
            .HasForeignKey(m => m.TemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Thread)
            .WithMany(t => t.OutboundMessages)
            .HasForeignKey(m => m.ThreadId)
            .OnDelete(DeleteBehavior.Restrict);

        // §10 indexes required for FR-010 (paginated/indexed inbox at 50k-message scale).
        builder.HasIndex(m => new { m.Status, m.NextRetryAt });
        builder.HasIndex(m => new { m.ThreadId, m.CreatedAt });
    }
}
