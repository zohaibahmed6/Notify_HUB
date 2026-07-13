namespace NotifyHub.Api.Users.Dtos;

public class CreateUserRequest
{
    public string Username { get; set; } = default!;
    public string? FullName { get; set; }
    public string Password { get; set; } = default!;
    public string Role { get; set; } = default!;
}
