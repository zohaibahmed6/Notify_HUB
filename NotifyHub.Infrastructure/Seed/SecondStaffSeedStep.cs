using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Domain.Validation;
using NotifyHub.Infrastructure.Persistence;

namespace NotifyHub.Infrastructure.Seed;

/// Seeds a second Staff user, for testing multi-staff scenarios (assign-to-me across
/// two distinct accounts, two-tab SignalR checks as different identities) that
/// UserSeedStep's single Admin+Staff pair can't exercise. Idempotent by username check
/// (not "any user exists") so it still runs against an already-seeded database.
public class SecondStaffSeedStep(IConfiguration configuration, IPasswordHasher<User> passwordHasher) : IDbSeedStep
{
    public async Task RunAsync(NotifyHubDbContext db, CancellationToken ct)
    {
        var username = configuration["Seed:Staff2Username"];
        if (string.IsNullOrWhiteSpace(username))
            return;

        if (await db.Users.AnyAsync(u => u.Username == username, ct))
            return;

        var password = configuration["Seed:Staff2Password"];
        if (!PasswordPolicy.IsValid(password, out var failures))
        {
            throw new InvalidOperationException(
                $"Seed password for 'Seed:Staff2Password' does not meet the password policy: {string.Join(" ", failures)}");
        }

        var user = new User { Username = username, FullName = "David Lee", Role = UserRole.Staff };
        user.PasswordHash = passwordHasher.HashPassword(user, password!);

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
    }
}
