using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotifyHub.Domain.Entities;

namespace NotifyHub.Infrastructure.Persistence.Configurations;

public class MessageTemplateConfiguration : IEntityTypeConfiguration<MessageTemplate>
{
    public void Configure(EntityTypeBuilder<MessageTemplate> builder)
    {
        builder.ToTable("message_templates");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.Body)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(t => t.OffsetHours).IsRequired();

        builder.Property(t => t.IsActive).IsRequired();

        builder.Property(t => t.CommunicationMode)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // No existing many-to-many precedent in this codebase — EF Core's implicit
        // skip-navigation join (no custom join entity class) is the simplest fit for
        // "which bookmarks does this template include."
        builder.HasMany(t => t.Bookmarks)
            .WithMany(b => b.Templates)
            .UsingEntity(j => j.ToTable("message_template_bookmarks"));
    }
}
