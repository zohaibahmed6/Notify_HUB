namespace NotifyHub.Api.Templates.Dtos;

/// PATCH semantics: only non-null fields are applied, same convention as UpdateTaskRequest.
public class UpdateTemplateRequest
{
    public string? Name { get; set; }
    public string? Body { get; set; }
    public int? OffsetHours { get; set; }
    public bool? IsActive { get; set; }
    public string? CommunicationMode { get; set; }

    /// Full-replace semantics when provided (same convention as
    /// TaskForwardingRulesController's PATCH) — not a sparse add/remove.
    public IReadOnlyList<long>? BookmarkIds { get; set; }
}
