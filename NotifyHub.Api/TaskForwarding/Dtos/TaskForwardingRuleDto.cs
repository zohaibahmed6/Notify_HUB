namespace NotifyHub.Api.TaskForwarding.Dtos;

public class TaskForwardingRuleDto
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string Username { get; set; } = default!;
    public string? FullName { get; set; }
    public string Role { get; set; } = default!;
    public long TargetUserId { get; set; }
    public string TargetUsername { get; set; } = default!;
    public string? TargetFullName { get; set; }
    public string TargetRole { get; set; } = default!;
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// Shared shape for create and edit (rule 10 — rules can be edited any time) — edit is a
/// full replace of the from-user/target/window/reason, not a sparse PATCH, since From/To
/// (the date window) are themselves nullable and a sparse-PATCH "did the caller mean to
/// clear this?" ambiguity isn't worth the complexity for what's a small, infrequently-edited
/// config object. `UserId` (the "From" user whose tasks get forwarded) was caller-implicit
/// before this change opened the feature up to letting any user configure rules for anyone.
public class TaskForwardingRuleRequest
{
    public long UserId { get; set; }
    public long TargetUserId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Reason { get; set; }
}
