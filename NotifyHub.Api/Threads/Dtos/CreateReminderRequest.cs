using System.ComponentModel.DataAnnotations;

namespace NotifyHub.Api.Threads.Dtos;

/// P9-08: no manual "scheduled send time" field (rule 4) — that's always computed
/// server-side from EventTime + the current Reminder Offset setting (rule 5).
public class CreateReminderRequest
{
    public long TemplateId { get; set; }
    public DateTime EventTime { get; set; }

    /// Reversal of the original rule 31 ("read-only preview, never committed ad-hoc"):
    /// the Reminder SMS dialog's body is now freely editable, so the caller-edited text
    /// is committed as RenderedBody at creation instead of staying null/rendered-fresh-
    /// at-dispatch. Optional (not [Required]) purely for backward compatibility with any
    /// caller that omits it — omitting it preserves the original "null RenderedBody,
    /// rendered fresh from the live template at dispatch" behavior unchanged.
    [MaxLength(1000)]
    public string? Body { get; set; }
}
