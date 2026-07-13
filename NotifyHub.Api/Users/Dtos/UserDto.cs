namespace NotifyHub.Api.Users.Dtos;

public class UserDto
{
    public long Id { get; set; }
    public string Username { get; set; } = default!;
    public string? FullName { get; set; }
    public string Role { get; set; } = default!;
    public string Status { get; set; } = default!;
}
