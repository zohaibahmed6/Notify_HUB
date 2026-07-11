using System.ComponentModel.DataAnnotations;

namespace NotifyHub.Api.Threads.Dtos;

public class ReplyRequest
{
    [Required]
    [MaxLength(1000)]
    public string Body { get; set; } = default!;
}
