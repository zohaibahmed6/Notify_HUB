namespace NotifyHub.Domain.Enums;

/// Named NotifyHubTaskStatus (not TaskStatus) to avoid clashing with System.Threading.Tasks.
/// Cancelled is not in §11a's original enum list — added per BR-007b, which requires a way
/// to end a recurring series without completing it; see STATUS.md documented deviation.
public enum NotifyHubTaskStatus
{
    Open,
    InProgress,
    Completed,
    Escalated,
    Cancelled
}
