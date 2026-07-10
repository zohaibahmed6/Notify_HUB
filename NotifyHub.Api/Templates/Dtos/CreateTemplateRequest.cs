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

    [Required]
    public string TriggerType { get; set; } = default!;

    [Range(1, int.MaxValue)]
    public int OffsetHours { get; set; }
}
