using System.ComponentModel.DataAnnotations;

namespace NotifyHub.Api.Webhooks.Dtos;

public class GatewayReceiptRequest
{
    [Required]
    public long MessageId { get; set; }

    public bool Delivered { get; set; }
}
