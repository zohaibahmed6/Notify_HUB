namespace NotifyHub.Domain.Entities;

/// Stub entity (§7) — synthetic patient data only, no real PHI (BR-006). Feeds FR-009.
public class Patient
{
    public long Id { get; set; }
    public string Name { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public DateTime? OptOutAt { get; set; }

    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
