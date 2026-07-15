using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NotifyHub.Domain.Entities;

namespace NotifyHub.Infrastructure.Persistence;

public class NotifyHubDbContext(DbContextOptions<NotifyHubDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<MessageTemplate> MessageTemplates => Set<MessageTemplate>();
    public DbSet<OutboundMessage> OutboundMessages => Set<OutboundMessage>();
    public DbSet<DeliveryStatusHistory> DeliveryStatusHistories => Set<DeliveryStatusHistory>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ConversationThread> Threads => Set<ConversationThread>();
    public DbSet<InboundMessage> InboundMessages => Set<InboundMessage>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<Bookmark> Bookmarks => Set<Bookmark>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<TaskForwardingRule> TaskForwardingRules => Set<TaskForwardingRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotifyHubDbContext).Assembly);
    }

    // Pomelo/MySQL drops DateTimeKind on round-trip — every DateTime column comes back
    // Unspecified regardless of how it was written, which makes System.Text.Json omit the
    // 'Z' suffix and silently breaks the frontend's UTC->local conversion (PROJECT_CONTEXT.md
    // §11a: all timestamps are stored/written as UTC). Applied model-wide so every current and
    // future DateTime column is covered, instead of remembering to add this per entity config.
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcNullableDateTimeConverter>();
    }

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private sealed class UtcNullableDateTimeConverter() : ValueConverter<DateTime?, DateTime?>(
        v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
}
