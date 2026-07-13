namespace NotifyHub.Api.Templates.Dtos;

/// PATCH semantics: only non-null fields are applied, same convention as UpdateTaskRequest.
public class UpdateTemplateRequest
{
    public string? Name { get; set; }
    public string? Body { get; set; }
    public string? TriggerType { get; set; }
    public int? OffsetHours { get; set; }
    public bool? IsActive { get; set; }
}
