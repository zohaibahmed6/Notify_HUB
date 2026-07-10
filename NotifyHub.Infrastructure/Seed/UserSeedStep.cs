using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Domain.Validation;
using NotifyHub.Infrastructure.Persistence;

namespace NotifyHub.Infrastructure.Seed;

/// Seeds exactly one Admin and one Staff user from configuration (never hardcoded).
/// Idempotent: skips entirely if any user already exists.
public class UserSeedStep(IConfiguration configuration, IPasswordHasher<User> passwordHasher) : IDbSeedStep
{
    public async Task RunAsync(NotifyHubDbContext db, CancellationToken ct)
    {
        if (await db.Users.AnyAsync(ct))
            return;

        var admin = BuildUser(
            usernameKey: "Seed:AdminUsername",
            passwordKey: "Seed:AdminPassword",
            role: UserRole.Admin);

        var staff = BuildUser(
            usernameKey: "Seed:StaffUsername",
            passwordKey: "Seed:StaffPassword",
            role: UserRole.Staff);

        db.Users.AddRange(admin, staff);
        await db.SaveChangesAsync(ct);
    }

    private User BuildUser(string usernameKey, string passwordKey, UserRole role)
    {
        var username = configuration[usernameKey];
        var password = configuration[passwordKey];

        if (string.IsNullOrWhiteSpace(username))
            throw new InvalidOperationException($"Missing required seed configuration: {usernameKey}");

        if (!PasswordPolicy.IsValid(password, out var failures))
        {
            throw new InvalidOperationException(
                $"Seed password for '{usernameKey}' does not meet the password policy: {string.Join(" ", failures)}");
        }

        var user = new User { Username = username, Role = role };
        user.PasswordHash = passwordHasher.HashPassword(user, password!);
        return user;
    }
}
