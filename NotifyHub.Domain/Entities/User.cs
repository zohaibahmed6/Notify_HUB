using NotifyHub.Domain.Enums;

namespace NotifyHub.Domain.Entities;

public class User
{
    public long Id { get; set; }
    public string Username { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public UserRole Role { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
