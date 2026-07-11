using System.ComponentModel.DataAnnotations;

namespace NotifyHub.Api.Webhooks.Dtos;

/// FR-005: simulated patient reply. Routed to the patient's thread by phone number,
/// since that's the only identifier a real inbound SMS carries.
public class InboundMessageRequest
{
    [Required]
    public string Phone { get; set; } = default!;

    [Required]
    [MaxLength(1000)]
    public string Body { get; set; } = default!;
}
