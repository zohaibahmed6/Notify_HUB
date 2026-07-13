namespace NotifyHub.Api.Tasks.Dtos;

public class ForwardTaskRequest
{
    public long TargetUserId { get; set; }
    public string? Note { get; set; }
}
