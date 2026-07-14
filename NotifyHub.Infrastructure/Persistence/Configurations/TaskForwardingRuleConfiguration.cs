using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotifyHub.Domain.Entities;

namespace NotifyHub.Infrastructure.Persistence.Configurations;

public class TaskForwardingRuleConfiguration : IEntityTypeConfiguration<TaskForwardingRule>
{
    public void Configure(EntityTypeBuilder<TaskForwardingRule> builder)
    {
        builder.ToTable("task_forwarding_rules");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Reason).HasMaxLength(300);
        builder.Property(r => r.CreatedAt).IsRequired();

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.TargetUser)
            .WithMany()
            .HasForeignKey(r => r.TargetUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Overlap prevention (rule 4/9) is enforced in the controller, not the DB — MySQL
        // has no exclusion-constraint equivalent for date-range overlap. This index just
        // speeds up the per-user lookup both the controller's overlap check and
        // FallbackUserResolver's resolution query perform.
        builder.HasIndex(r => r.UserId);
    }
}
