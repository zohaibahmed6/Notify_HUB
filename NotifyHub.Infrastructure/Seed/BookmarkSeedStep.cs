using Microsoft.EntityFrameworkCore;
using NotifyHub.Domain.Entities;
using NotifyHub.Infrastructure.Persistence;

namespace NotifyHub.Infrastructure.Seed;

/// §5: seeds the two merge fields TemplateRenderer actually resolves at send time
/// (patient_name always, appointment_time for AppointmentReminder sends) — deliberately
/// not fabricating bookmarks for fields the backend wouldn't fill in. Idempotent: skips
/// entirely if any bookmark already exists.
public class BookmarkSeedStep : IDbSeedStep
{
    public async Task RunAsync(NotifyHubDbContext db, CancellationToken ct)
    {
        if (await db.Bookmarks.AnyAsync(ct))
            return;

        db.Bookmarks.AddRange(
            new Bookmark
            {
                Label = "Patient Name",
                Description = "Inserts the patient's name.",
                InsertText = "{{patient_name}}",
            },
            new Bookmark
            {
                Label = "Appointment Time",
                Description = "Inserts the appointment date/time (appointment reminder templates only).",
                InsertText = "{{appointment_time}}",
            });

        await db.SaveChangesAsync(ct);
    }
}
