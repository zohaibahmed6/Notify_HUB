using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotifyHub.Domain.Entities;

namespace NotifyHub.Infrastructure.Persistence.Configurations;

public class ConversationThreadConfiguration : IEntityTypeConfiguration<ConversationThread>
{
    public void Configure(EntityTypeBuilder<ConversationThread> builder)
    {
        builder.ToTable("threads");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.UnreadCount).IsRequired();

        builder.HasOne(t => t.Patient)
            .WithMany()
            .HasForeignKey(t => t.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(t => t.PatientId).IsUnique();

        builder.HasOne(t => t.AssignedStaff)
            .WithMany()
            .HasForeignKey(t => t.AssignedStaffId)
            .OnDelete(DeleteBehavior.Restrict);

        // §7: required for paginated/indexed inbox (FR-010).
        builder.HasIndex(t => t.AssignedStaffId);
    }
}
