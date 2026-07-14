using System.ComponentModel.DataAnnotations;

namespace NotifyHub.Api.Webhooks.Dtos;

public class GatewayReceiptRequest
{
    [Required]
    public long MessageId { get; set; }

    public bool Delivered { get; set; }

    /// P9-09: segment count as computed by the "carrier" (mock gateway) from the actual
    /// sent text — sourced from the receipt, not recomputed by NotifyHub's own dispatcher.
    public int? PduCount { get; set; }
}
