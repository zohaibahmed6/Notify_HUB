namespace NotifyHub.Api.Threads.Dtos;

public class ThreadDetailDto : ThreadDto
{
    public IReadOnlyList<ThreadMessageDto> Messages { get; set; } = [];
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
}
