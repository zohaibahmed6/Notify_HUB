namespace NotifyHub.Api.Messages.Dtos;

public class SmsHistoryDto
{
    public long Id { get; set; }
    public string PatientName { get; set; } = default!;

    /// "System" for templated/system sends (SentByUsername is null) — P9-06.
    public string SenderUsername { get; set; } = default!;
    public string Phone { get; set; } = default!;

    /// RenderedBody — null while still Queued/Sending and not yet rendered.
    public string? Text { get; set; }
    public string Status { get; set; } = default!;

    /// Falls back to CreatedAt for a direct (immediate) send — same `scheduledAt ??
    /// createdAt` anchor MessageExpiryCalculator already uses, so this is never null.
    public DateTime ScheduledTime { get; set; }

    /// Populated once P9-07 adds OutboundMessage.ExpiresAt.
    public DateTime? ExpiryTime { get; set; }

    /// Populated once P9-09 adds OutboundMessage.PduCount — null/pending until a delivery
    /// receipt lands.
    public int? PduCount { get; set; }
}

public class SmsHistoryPagedResult
{
    public IReadOnlyList<SmsHistoryDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }

    /// Total rows matching the current filter (any status) — also serves as the report's
    /// "Total SMS count" summary figure, since it's already scoped to the active filter.
    public int TotalCount { get; set; }

    /// Sum of PduCount across every row matching the current filter (not just the current
    /// page) — rows with no receipt yet contribute 0. Always 0 until P9-09 adds PduCount.
    public int TotalPduCount { get; set; }
}
