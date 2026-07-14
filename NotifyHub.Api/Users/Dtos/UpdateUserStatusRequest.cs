namespace NotifyHub.Api.Users.Dtos;

public class UpdateUserStatusRequest
{
    public string Status { get; set; } = default!;

    /// P9-12: required together when Status is "OnLeave".
    public DateTime? LeaveFrom { get; set; }
    public DateTime? LeaveTo { get; set; }
}
