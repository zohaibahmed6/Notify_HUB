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
    // Realistic sample names (not real patient data — BR-006) instead of "Patient 01"..
    // placeholders, so seeded demo data reads like a real clinic roster. Balanced across
    // Pakistani English / Indian / Chinese / Japanese locales (3/3/2/2).
    private static readonly string[] PatientNames =
    [
        "Ahmed Raza",
        "Ayesha Malik",
        "Bilal Sheikh",
        "Priya Sharma",
        "Rohan Mehta",
        "Ananya Iyer",
        "Wei Zhang",
        "Mei Chen",
        "Haruto Sato",
        "Yui Tanaka",
    ];

    public async Task RunAsync(NotifyHubDbContext db, CancellationToken ct)
    {
        if (await db.Patients.AnyAsync(ct))
            return;

        var now = DateTime.UtcNow;
        var patients = new List<Patient>();

        for (var i = 1; i <= PatientNames.Length; i++)
        {
            var patient = new Patient
            {
                Name = PatientNames[i - 1],
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
