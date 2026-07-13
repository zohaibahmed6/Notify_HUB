using System.ComponentModel.DataAnnotations;

namespace NotifyHub.Api.Threads.Dtos;

public class ReplyRequest
{
    [Required]
    [MaxLength(1000)]
    public string Body { get; set; } = default!;

    /// §6: future-send time, must be strictly after now if provided.
    public DateTime? ScheduledAt { get; set; }
}
