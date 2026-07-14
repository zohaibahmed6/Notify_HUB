using NotifyHub.Domain.Enums;

namespace NotifyHub.Domain.Entities;

public class OutboundMessage
{
    public long Id { get; set; }

    public long PatientId { get; set; }
    public Patient Patient { get; set; } = default!;

    /// Null for system-dispatched messages created before a thread exists for the patient
    /// (e.g. seeded demo messages); set once a thread is found-or-created (step 3+).
    public long? ThreadId { get; set; }
    public ConversationThread? Thread { get; set; }

    /// Null means an ad-hoc staff reply (no template) — see BR-008. Ad-hoc replies have
    /// RenderedBody set directly at creation (no template to render at send time).
    public long? TemplateId { get; set; }
    public MessageTemplate? Template { get; set; }

    public SenderType SenderType { get; set; }

    /// P9-06: denormalized snapshot of the staff username who sent this (SenderType.Staff
    /// only) — set at creation time (Reply/CreateConversation), same "plain string, not a
    /// live FK lookup" convention as AuditLog.Actor. Null for SenderType.System sends; the
    /// SMS History report shows "System" in that case.
    public string? SentByUsername { get; set; }

    /// Business event string (e.g. "appointment:{id}:created"); null for ad-hoc staff replies. See BR-009.
    public string? TriggerReference { get; set; }

    /// Rendered server-side at send time, snapshotted here so audit history reflects what was
    /// actually sent even if the template is edited afterward (BR-013). Null until dispatched.
    public string? RenderedBody { get; set; }

    public DateTime CreatedAt { get; set; }
    public MessageStatus Status { get; set; }

    /// SHA-256 hex of patientId+templateId+triggerReference; unique, required only for
    /// system-dispatched messages (§9, FR-003).
    public string? IdempotencyKey { get; set; }

    public int AttemptCount { get; set; }
    public DateTime? NextRetryAt { get; set; }

    /// §6: future-send time for staff-initiated messages; null means "send as soon as
    /// dispatched" (existing default behavior, unchanged). Distinct from NextRetryAt, which
    /// is purely a retry-backoff timestamp (BR-011).
    public DateTime? ScheduledAt { get; set; }

    /// P9-07: Standard SMS default expiry is 12h from CreatedAt (immediate sends) or
    /// ScheduledAt (scheduled sends) — computed and stored at creation
    /// (MessageExpiryCalculator). Nullable rather than the plan's literal "(DateTime)":
    /// pre-P9-07 rows have nothing to backfill from, and the dispatcher's expiry sweep
    /// only ever queries Status == Queued rows anyway, where a null ExpiresAt just means
    /// "never expires" (matches every pre-existing creation path this increment didn't
    /// touch, e.g. the old ReminderScheduler — retired in P9-08, so left alone here).
    public DateTime? ExpiresAt { get; set; }

    /// Set only when Status transitions to Expired; null otherwise.
    public string? ExpiryReason { get; set; }

    /// P9-08: set only for Reminder SMS (rules 3/32) — the caller-supplied instant the
    /// reminder is anchored to. Null for Standard SMS. Deliberately not an Appointment FK
    /// (rule 34: generic, reusable for any future event-based reminder).
    public DateTime? EventTime { get; set; }

    /// Snapshotted from SettingsService at creation time (rule 7 — a later Settings change
    /// never applies retroactively) and reused verbatim when EventTime is edited later
    /// (rule 26), not re-read from current Settings.
    public int? ReminderOffsetMinutes { get; set; }
    public int? ReminderExpiryOffsetMinutes { get; set; }

    /// Rule 32 "Sent Time" — set once, when Status first transitions to Sent
    /// (MockGatewayController.Send). Applies to both Standard and Reminder SMS alike
    /// (rule 22: same pipeline), not Reminder-specific storage.
    public DateTime? SentAt { get; set; }

    public ICollection<DeliveryStatusHistory> StatusHistory { get; set; } = new List<DeliveryStatusHistory>();
}
