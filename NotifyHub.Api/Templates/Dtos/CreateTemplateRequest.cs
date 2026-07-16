using System.ComponentModel.DataAnnotations;

namespace NotifyHub.Api.Templates.Dtos;

public class CreateTemplateRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = default!;

    [Required]
    [MaxLength(1000)]
    public string Body { get; set; } = default!;

    [Range(1, int.MaxValue)]
    public int OffsetHours { get; set; }

    /// Optional so existing callers that predate this field keep working — defaults to
    /// Sms server-side when omitted (TemplatesController.Create).
    public string? CommunicationMode { get; set; }

    public IReadOnlyList<long>? BookmarkIds { get; set; }
}
