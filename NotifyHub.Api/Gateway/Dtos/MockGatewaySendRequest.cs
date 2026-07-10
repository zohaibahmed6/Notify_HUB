using System.ComponentModel.DataAnnotations;

namespace NotifyHub.Api.Gateway.Dtos;

public class MockGatewaySendRequest
{
    [Required]
    public long MessageId { get; set; }
}
