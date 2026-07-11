namespace NotifyHub.Api.Auth.Dtos;

public class AuthResponse
{
    public string AccessToken { get; set; } = default!;
    public DateTime AccessTokenExpiresAt { get; set; }
    public AuthUserDto User { get; set; } = default!;
}

public class AuthUserDto
{
    public long Id { get; set; }
    public string Username { get; set; } = default!;
    public string Role { get; set; } = default!;
}
