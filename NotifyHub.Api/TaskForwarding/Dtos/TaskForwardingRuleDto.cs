namespace NotifyHub.Api.TaskForwarding.Dtos;

public class TaskForwardingRuleDto
{
    public long Id { get; set; }
    public long TargetUserId { get; set; }
    public string TargetUsername { get; set; } = default!;
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// Shared shape for create and edit (rule 10 — rules can be edited any time) — edit is a
/// full replace of the target/window/reason, not a sparse PATCH, since From/To are
/// themselves nullable and a sparse-PATCH "did the caller mean to clear this?" ambiguity
/// isn't worth the complexity for what's a small, infrequently-edited config object.
public class TaskForwardingRuleRequest
{
    public long TargetUserId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Reason { get; set; }
}
