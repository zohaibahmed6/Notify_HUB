using System.ComponentModel.DataAnnotations;

namespace NotifyHub.Api.Auth.Dtos;

public class RefreshRequest
{
    [Required]
    public string RefreshToken { get; set; } = default!;
}
