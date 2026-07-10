using Microsoft.EntityFrameworkCore;
using NotifyHub.Domain.Entities;

namespace NotifyHub.Infrastructure.Persistence;

public class NotifyHubDbContext(DbContextOptions<NotifyHubDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotifyHubDbContext).Assembly);
    }
}
