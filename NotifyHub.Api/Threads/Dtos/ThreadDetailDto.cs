using NotifyHub.Api.Common;

namespace NotifyHub.Api.Threads.Dtos;

public class ThreadDetailDto : ThreadDto
{
    /// FR-010: paginated, not the thread's full message history — see
    /// ThreadsController.GetMessagesPageAsync.
    public PagedResult<ThreadMessageDto> Messages { get; set; } = new();
}

/// BR-008: templated (system) and ad-hoc (staff) outbound messages, plus inbound
/// patient replies, rendered together in thread order.
public class ThreadMessageDto
{
    public string Direction { get; set; } = default!; // "inbound" | "outbound"
    public string? SenderType { get; set; } // outbound only: "System" | "Staff"
    public string Body { get; set; } = default!;
    public DateTime Timestamp { get; set; }
    public string? Status { get; set; } // outbound only
    public DateTime? EventTime { get; set; } // outbound only, Reminder SMS only — marks this as a reminder
    public DateTime? ScheduledAt { get; set; } // outbound only: when a Queued message will actually dispatch
}
