using NotifyHub.Domain.Enums;

namespace NotifyHub.Domain.Entities;

public class User
{
    public long Id { get; set; }
    public string Username { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public UserRole Role { get; set; }

    /// Display name distinct from the login Username; falls back to Username when unset.
    public string? FullName { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Active;

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
