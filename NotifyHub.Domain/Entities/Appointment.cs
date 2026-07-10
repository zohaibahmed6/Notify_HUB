using NotifyHub.Domain.Enums;

namespace NotifyHub.Domain.Entities;

/// Stub entity (§7) — feeds FR-009 (reminder scheduling); no appointment management UI.
public class Appointment
{
    public long Id { get; set; }
    public long PatientId { get; set; }
    public Patient Patient { get; set; } = default!;

    public DateTime ScheduledAt { get; set; }
    public AppointmentStatus Status { get; set; }
}
