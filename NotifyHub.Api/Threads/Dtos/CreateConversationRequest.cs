using System.ComponentModel.DataAnnotations;

namespace NotifyHub.Api.Threads.Dtos;

/// §6: staff-initiated conversation with a brand-new patient — creates the Patient +
/// ConversationThread + first OutboundMessage in one call.
public class CreateConversationRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = default!;

    [Required]
    [MaxLength(20)]
    public string Phone { get; set; } = default!;

    [Required]
    [MaxLength(1000)]
    public string Message { get; set; } = default!;

    public DateTime? ScheduledAt { get; set; }
}
