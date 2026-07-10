using System.ComponentModel.DataAnnotations;

namespace NotifyHub.Api.Auth.Dtos;

public class LoginRequest
{
    [Required]
    public string Username { get; set; } = default!;

    [Required]
    public string Password { get; set; } = default!;
}
