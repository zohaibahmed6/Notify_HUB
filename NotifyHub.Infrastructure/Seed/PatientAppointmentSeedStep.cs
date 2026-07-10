using Microsoft.EntityFrameworkCore;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Infrastructure.Persistence;

namespace NotifyHub.Infrastructure.Seed;

/// Synthetic patients + stub appointments (BR-006: no real patient data). Idempotent:
/// skips entirely if any patient already exists. Small, fixed-size demo set — the
/// 50k-message performance seed is a separate, later step (FR-010).
public class PatientAppointmentSeedStep : IDbSeedStep
{
    private const int PatientCount = 10;

    public async Task RunAsync(NotifyHubDbContext db, CancellationToken ct)
    {
        if (await db.Patients.AnyAsync(ct))
            return;

        var now = DateTime.UtcNow;
        var patients = new List<Patient>();

        for (var i = 1; i <= PatientCount; i++)
        {
            var patient = new Patient
            {
                Name = $"Patient {i:D2}",
                Phone = $"+15550100{i:D3}",
            };

            patient.Appointments.Add(new Appointment
            {
                ScheduledAt = now.AddDays(i),
                Status = AppointmentStatus.Scheduled,
            });

            patients.Add(patient);
        }

        db.Patients.AddRange(patients);
        await db.SaveChangesAsync(ct);
    }
}
