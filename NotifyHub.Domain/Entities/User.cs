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

    /// P9-12: both required together when Status is set to OnLeave (validated in
    /// UsersController.UpdateStatus). Not cleared on transitioning away from OnLeave —
    /// left as a historical record, same "permanently retained" philosophy as audit
    /// history elsewhere — and overwritten with fresh values the next time this user
    /// goes OnLeave again.
    public DateTime? LeaveFrom { get; set; }
    public DateTime? LeaveTo { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
