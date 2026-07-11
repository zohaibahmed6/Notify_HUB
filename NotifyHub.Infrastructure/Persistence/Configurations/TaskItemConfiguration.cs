using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotifyHub.Domain.Entities;

namespace NotifyHub.Infrastructure.Persistence.Configurations;

public class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("tasks");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Priority)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.DueAt).IsRequired();

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.OccurrenceCount).IsRequired();

        builder.HasOne(t => t.Thread)
            .WithMany(th => th.Tasks)
            .HasForeignKey(t => t.ThreadId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.AssignedStaff)
            .WithMany()
            .HasForeignKey(t => t.AssignedStaffId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.OriginalOwner)
            .WithMany()
            .HasForeignKey(t => t.OriginalOwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Drives the escalation job (§11): flags tasks past due_at not already escalated.
        builder.HasIndex(t => new { t.Status, t.DueAt });
        builder.HasIndex(t => t.AssignedStaffId);
    }
}
