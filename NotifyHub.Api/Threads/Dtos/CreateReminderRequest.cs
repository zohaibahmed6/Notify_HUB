namespace NotifyHub.Api.Threads.Dtos;

/// P9-08: no manual "scheduled send time" field (rule 4) — that's always computed
/// server-side from EventTime + the current Reminder Offset setting (rule 5).
public class CreateReminderRequest
{
    public long TemplateId { get; set; }
    public DateTime EventTime { get; set; }
}
