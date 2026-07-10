namespace NotifyHub.Domain.Entities;

public class RefreshToken
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public User User { get; set; } = default!;

    /// SHA-256 hex of the raw opaque token — the raw value is never persisted.
    public string TokenHash { get; set; } = default!;

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    /// Rotation chain: when this token is used to refresh, the new token's id is recorded here
    /// and RevokedAt is set, so a replayed old token can be detected.
    public long? ReplacedByTokenId { get; set; }

    public bool IsActive => RevokedAt is null && DateTime.UtcNow < ExpiresAt;
}
