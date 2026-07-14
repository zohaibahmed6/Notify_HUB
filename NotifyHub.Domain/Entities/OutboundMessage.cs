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

    public ICollection<DeliveryStatusHistory> StatusHistory { get; set; } = new List<DeliveryStatusHistory>();
}
