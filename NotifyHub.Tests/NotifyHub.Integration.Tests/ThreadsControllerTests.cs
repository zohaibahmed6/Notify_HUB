using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NotifyHub.Api.Common;
using NotifyHub.Api.Threads.Dtos;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Infrastructure.Persistence;
using Xunit;

namespace NotifyHub.Integration.Tests;

/// FR-005/FR-007/FR-008/BR-001b/BR-012: thread reply, assign, and task-creation flows.
/// Each test creates its own patient/thread directly via DbContext (distinct phone
/// numbers, outside the 10 seeded demo patients) — all methods in this class share one
/// DB via IClassFixture, so isolated fixtures avoid cross-test contamination.
public class ThreadsControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Assign_Succeeds_WhenUnassigned()
    {
        var thread = await CreateThreadAsync("+19990000001");
        var (client, staffId) = await _client.AsStaffAsync();

        var response = await client.PostAsync($"/api/threads/{thread.Id}/assign", JsonContent.Create(new { }));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
        var updated = await db.Threads.SingleAsync(t => t.Id == thread.Id);
        Assert.Equal(staffId, updated.AssignedStaffId);

        var audit = await db.AuditLogs.SingleAsync(a => a.EntityType == "Thread" && a.EntityId == thread.Id && a.Action == "assignment");
        Assert.Equal(CustomWebApplicationFactory.StaffUsername, audit.Actor);
    }

    [Fact]
    public async Task Assign_ReturnsConflict_WhenAlreadyAssigned()
    {
        var thread = await CreateThreadAsync("+19990000002");
        var (_, adminId) = await factory.CreateClient().AsAdminAsync();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
            var toAssign = await db.Threads.SingleAsync(t => t.Id == thread.Id);
            toAssign.AssignedStaffId = adminId;
            await db.SaveChangesAsync();
        }

        var (client, _) = await _client.AsStaffAsync();
        var response = await client.PostAsync($"/api/threads/{thread.Id}/assign", JsonContent.Create(new { }));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Assign_StaffCannotAssignToAnotherUser()
    {
        var thread = await CreateThreadAsync("+19990000003");
        var (client, _) = await _client.AsStaffAsync();

        var response = await client.PostAsync($"/api/threads/{thread.Id}/assign", JsonContent.Create(new { staffId = 999999L }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Reply_BlockedWhenPatientOptedOut()
    {
        var thread = await CreateThreadAsync("+19990000004", patientOptedOut: true);
        var (client, _) = await _client.AsStaffAsync();

        var response = await client.PostAsJsonAsync($"/api/threads/{thread.Id}/messages", new ReplyRequest { Body = "hello" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reply_Succeeds_QueuesOutboundMessage()
    {
        var thread = await CreateThreadAsync("+19990000005");
        var (client, _) = await _client.AsStaffAsync();

        var response = await client.PostAsJsonAsync($"/api/threads/{thread.Id}/messages", new ReplyRequest { Body = "On our way, see you soon." });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
        var message = await db.OutboundMessages.SingleAsync(m => m.ThreadId == thread.Id);

        Assert.Equal("On our way, see you soon.", message.RenderedBody);
        Assert.Null(message.TemplateId);
        Assert.Equal(NotifyHub.Domain.Enums.SenderType.Staff, message.SenderType);
    }

    [Fact]
    public async Task Detail_ResetsUnreadCountToZero()
    {
        var thread = await CreateThreadAsync("+19990000006", initialUnreadCount: 3);
        var (client, _) = await _client.AsStaffAsync();

        var response = await client.GetAsync($"/api/threads/{thread.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ThreadDetailDto>();
        Assert.Equal(0, body!.UnreadCount);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
        var updated = await db.Threads.SingleAsync(t => t.Id == thread.Id);
        Assert.Equal(0, updated.UnreadCount);
    }

    [Fact]
    public async Task Detail_PaginatesMessages_DoesNotReturnFullHistory()
    {
        var thread = await CreateThreadAsync("+19990000008");
        const int totalMessages = 60;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
            var baseTime = DateTime.UtcNow.AddDays(-1);

            for (var i = 0; i < totalMessages; i++)
            {
                var timestamp = baseTime.AddMinutes(i);
                if (i % 2 == 0)
                {
                    db.OutboundMessages.Add(new OutboundMessage
                    {
                        PatientId = thread.PatientId,
                        ThreadId = thread.Id,
                        SenderType = Domain.Enums.SenderType.Staff,
                        RenderedBody = $"outbound-{i}",
                        CreatedAt = timestamp,
                        Status = Domain.Enums.MessageStatus.Delivered,
                    });
                }
                else
                {
                    db.InboundMessages.Add(new InboundMessage { ThreadId = thread.Id, Body = $"inbound-{i}", ReceivedAt = timestamp });
                }
            }
            await db.SaveChangesAsync();
        }

        var (client, _) = await _client.AsStaffAsync();

        // Defaults (no page/pageSize query params): §11a's 25/max-100 pattern.
        var page1Response = await client.GetAsync($"/api/threads/{thread.Id}");
        var page1 = await page1Response.Content.ReadFromJsonAsync<ThreadDetailDto>();

        Assert.Equal(25, page1!.Messages.Items.Count); // not all 60 — proves the full history isn't returned
        Assert.Equal(totalMessages, page1.Messages.TotalCount);

        var expectedPage1Bodies = Enumerable.Range(totalMessages - 25, 25)
            .Select(i => i % 2 == 0 ? $"outbound-{i}" : $"inbound-{i}")
            .ToList();
        Assert.Equal(expectedPage1Bodies, page1.Messages.Items.Select(m => m.Body).ToList());

        var page2Response = await client.GetAsync($"/api/threads/{thread.Id}?page=2");
        var page2 = await page2Response.Content.ReadFromJsonAsync<ThreadDetailDto>();

        Assert.Equal(25, page2!.Messages.Items.Count);

        // Combined, the two pages cover exactly the most recent 50 of 60 messages, in
        // chronological order, with zero overlap — proves the merge-pagination across the
        // two independently-ordered tables (inbound/outbound) is correct, not just "returns
        // some subset".
        var combinedBodies = page2.Messages.Items.Select(m => m.Body)
            .Concat(page1.Messages.Items.Select(m => m.Body))
            .ToList();
        var expectedCombinedBodies = Enumerable.Range(10, 50)
            .Select(i => i % 2 == 0 ? $"outbound-{i}" : $"inbound-{i}")
            .ToList();
        Assert.Equal(expectedCombinedBodies, combinedBodies);
    }

    [Fact]
    public async Task Detail_IncludesEventTimeAndScheduledAt_ForQueuedReminder_ButNotForPlainScheduledReply()
    {
        var thread = await CreateThreadAsync("+19990000110");
        var eventTime = DateTime.UtcNow.AddDays(2);
        var scheduledAt = DateTime.UtcNow.AddDays(1).AddHours(23); // reminder's own computed send time

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
            db.OutboundMessages.Add(new OutboundMessage
            {
                PatientId = thread.PatientId,
                ThreadId = thread.Id,
                SenderType = Domain.Enums.SenderType.Staff,
                RenderedBody = "Reminder body",
                CreatedAt = DateTime.UtcNow,
                Status = Domain.Enums.MessageStatus.Queued,
                EventTime = eventTime,
                ScheduledAt = scheduledAt,
            });
            // Plain staff-scheduled reply — Queued + ScheduledAt set, but no EventTime, since
            // it didn't come from the Reminder SMS flow.
            db.OutboundMessages.Add(new OutboundMessage
            {
                PatientId = thread.PatientId,
                ThreadId = thread.Id,
                SenderType = Domain.Enums.SenderType.Staff,
                RenderedBody = "Plain scheduled reply",
                CreatedAt = DateTime.UtcNow,
                Status = Domain.Enums.MessageStatus.Queued,
                ScheduledAt = DateTime.UtcNow.AddHours(2),
            });
            await db.SaveChangesAsync();
        }

        var (client, _) = await _client.AsStaffAsync();
        var response = await client.GetAsync($"/api/threads/{thread.Id}");
        var body = await response.Content.ReadFromJsonAsync<ThreadDetailDto>();

        var reminderMessage = body!.Messages.Items.Single(m => m.Body == "Reminder body");
        Assert.Equal("Queued", reminderMessage.Status);
        Assert.NotNull(reminderMessage.EventTime);
        Assert.Equal(eventTime, reminderMessage.EventTime!.Value, TimeSpan.FromSeconds(1));
        Assert.NotNull(reminderMessage.ScheduledAt);
        Assert.Equal(scheduledAt, reminderMessage.ScheduledAt!.Value, TimeSpan.FromSeconds(1));

        var plainScheduledMessage = body.Messages.Items.Single(m => m.Body == "Plain scheduled reply");
        Assert.Null(plainScheduledMessage.EventTime);
        Assert.NotNull(plainScheduledMessage.ScheduledAt);
    }

    [Fact]
    public async Task CreateTask_UsesDefaultPriorityAndDueDate_WhenNotSpecified()
    {
        var thread = await CreateThreadAsync("+19990000007");
        var (client, _) = await _client.AsStaffAsync();

        var response = await client.PostAsJsonAsync($"/api/threads/{thread.Id}/tasks", new { });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
        var task = await db.Tasks.SingleAsync(t => t.ThreadId == thread.Id);

        Assert.Equal(Domain.Enums.TaskPriority.Medium, task.Priority);
        Assert.True(task.DueAt > DateTime.UtcNow.AddDays(2) && task.DueAt < DateTime.UtcNow.AddDays(4));
    }

    [Fact]
    public async Task Reply_WithFutureScheduledAt_QueuesWithScheduledAtSet()
    {
        var thread = await CreateThreadAsync("+19990000101");
        var (client, _) = await _client.AsStaffAsync();
        var scheduledAt = DateTime.UtcNow.AddHours(2);

        var response = await client.PostAsJsonAsync($"/api/threads/{thread.Id}/messages", new { body = "Later", scheduledAt });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
        var message = await db.OutboundMessages.SingleAsync(m => m.ThreadId == thread.Id);

        Assert.NotNull(message.ScheduledAt);
        Assert.Equal(scheduledAt, message.ScheduledAt!.Value, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Reply_WithPastScheduledAt_Returns400()
    {
        var thread = await CreateThreadAsync("+19990000102");
        var (client, _) = await _client.AsStaffAsync();

        var response = await client.PostAsJsonAsync($"/api/threads/{thread.Id}/messages", new { body = "Late", scheduledAt = DateTime.UtcNow.AddHours(-1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateConversation_CreatesPatientThreadAndMessage()
    {
        var (client, _) = await _client.AsStaffAsync();

        var response = await client.PostAsJsonAsync("/api/threads", new
        {
            name = "Brand New Patient",
            phone = "+19990000103",
            message = "Welcome!",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<ThreadDto>();
        Assert.Equal("Brand New Patient", dto!.PatientName);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
        var patient = await db.Patients.SingleAsync(p => p.Phone == "+19990000103");
        var message = await db.OutboundMessages.SingleAsync(m => m.PatientId == patient.Id);
        Assert.Equal("Welcome!", message.RenderedBody);
    }

    [Fact]
    public async Task CreateConversation_DuplicatePhone_Returns409()
    {
        await CreateThreadAsync("+19990000104");
        var (client, _) = await _client.AsStaffAsync();

        var response = await client.PostAsJsonAsync("/api/threads", new
        {
            name = "Duplicate Phone Patient",
            phone = "+19990000104",
            message = "Hello",
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Reply_ExceedingRateLimit_Returns429()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var settingsService = scope.ServiceProvider.GetRequiredService<NotifyHub.Infrastructure.Settings.SettingsService>();
            await settingsService.SetAsync(new Dictionary<string, string>
            {
                [NotifyHub.Infrastructure.Settings.SettingsService.RateLimitEnabledKey] = "true",
                [NotifyHub.Infrastructure.Settings.SettingsService.RateLimitMaxMessagesKey] = "1",
                [NotifyHub.Infrastructure.Settings.SettingsService.RateLimitWindowHoursKey] = "24",
            }, CancellationToken.None);
        }

        try
        {
            var thread = await CreateThreadAsync("+19990000105");
            var (client, _) = await _client.AsStaffAsync();

            var first = await client.PostAsJsonAsync($"/api/threads/{thread.Id}/messages", new { body = "First" });
            Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

            var second = await client.PostAsJsonAsync($"/api/threads/{thread.Id}/messages", new { body = "Second" });
            Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<NotifyHub.Infrastructure.Settings.SettingsService>();
            await settingsService.SetAsync(new Dictionary<string, string>
            {
                [NotifyHub.Infrastructure.Settings.SettingsService.RateLimitEnabledKey] = "false",
            }, CancellationToken.None);
        }
    }

    /// P9-04: {{patient_name}} always resolves from the thread's real patient;
    /// {{appointment_time}} falls back to a generated future dummy time when the patient
    /// has no real upcoming Scheduled appointment.
    [Fact]
    public async Task PreviewTemplate_ResolvesPatientName_AndDummyAppointmentTime_WhenNoneExists()
    {
        var thread = await CreateThreadAsync("+19990000201");
        long templateId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
            var template = new MessageTemplate
            {
                Name = "Preview test template",
                Body = "Hi {{patient_name}}, see you at {{appointment_time}}.",
                OffsetHours = 48,
            };
            db.MessageTemplates.Add(template);
            await db.SaveChangesAsync();
            templateId = template.Id;
        }

        var (client, _) = await _client.AsStaffAsync();
        var response = await client.GetAsync($"/api/threads/{thread.Id}/templates/{templateId}/preview");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TemplatePreviewDto>();
        Assert.Contains("Test Patient +19990000201", body!.RenderedBody);
        Assert.DoesNotContain("{{appointment_time}}", body.RenderedBody);
        Assert.DoesNotContain("{{patient_name}}", body.RenderedBody);
    }

    [Fact]
    public async Task PreviewTemplate_ResolvesRealUpcomingAppointment_WhenOneExists()
    {
        var thread = await CreateThreadAsync("+19990000202");
        var scheduledAt = DateTime.UtcNow.AddDays(5);
        long templateId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
            db.Appointments.Add(new Appointment { PatientId = thread.PatientId, ScheduledAt = scheduledAt, Status = AppointmentStatus.Scheduled });
            var template = new MessageTemplate
            {
                Name = "Preview test template 2",
                Body = "See you at {{appointment_time}}.",
                OffsetHours = 48,
            };
            db.MessageTemplates.Add(template);
            await db.SaveChangesAsync();
            templateId = template.Id;
        }

        var (client, _) = await _client.AsStaffAsync();
        var response = await client.GetAsync($"/api/threads/{thread.Id}/templates/{templateId}/preview");

        var body = await response.Content.ReadFromJsonAsync<TemplatePreviewDto>();
        Assert.Contains(scheduledAt.ToString("u"), body!.RenderedBody);
    }

    /// Reminder SMS is deliberately Appointment-independent (STEP9_PLAN.md rule 34) — the
    /// isReminder=true flag must skip the real-Appointment lookup and leave
    /// {{appointment_time}} as a literal unresolved token, so the Reminder SMS dialog can
    /// substitute the staff member's own picked Event Time in client-side.
    [Fact]
    public async Task PreviewTemplate_WithIsReminderTrue_LeavesAppointmentTimeTokenUnresolved()
    {
        var thread = await CreateThreadAsync("+19990000203");
        var scheduledAt = DateTime.UtcNow.AddDays(5);
        long templateId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
            // Even with a real upcoming appointment present, isReminder=true must not use it.
            db.Appointments.Add(new Appointment { PatientId = thread.PatientId, ScheduledAt = scheduledAt, Status = AppointmentStatus.Scheduled });
            var template = new MessageTemplate
            {
                Name = "Preview test template 3",
                Body = "Hi {{patient_name}}, see you at {{appointment_time}}.",
                OffsetHours = 48,
            };
            db.MessageTemplates.Add(template);
            await db.SaveChangesAsync();
            templateId = template.Id;
        }

        var (client, _) = await _client.AsStaffAsync();
        var response = await client.GetAsync($"/api/threads/{thread.Id}/templates/{templateId}/preview?isReminder=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TemplatePreviewDto>();
        Assert.Contains("Test Patient +19990000203", body!.RenderedBody); // patient_name still resolved
        Assert.Contains("{{appointment_time}}", body.RenderedBody); // left unresolved, not the real appointment
        Assert.DoesNotContain(scheduledAt.ToString("u"), body.RenderedBody);
    }

    [Fact]
    public async Task List_SearchFiltersByPatientName_AcrossAllThreads_NotJustTheLoadedPage()
    {
        var target = await CreateThreadAsync("+19990000301", patientName: "Zzyzx Quorlax");
        await CreateThreadAsync("+19990000302", patientName: "Completely Different Person");

        var (client, _) = await _client.AsStaffAsync();
        var response = await client.GetAsync("/api/threads?search=Zzyzx");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<PagedResult<ThreadDto>>();

        Assert.NotNull(result);
        Assert.Contains(result!.Items, t => t.Id == target.Id);
        Assert.DoesNotContain(result.Items, t => t.PatientName == "Completely Different Person");
    }

    [Fact]
    public async Task List_SearchMatchesAssignedStaffUsername()
    {
        var thread = await CreateThreadAsync("+19990000303", patientName: "Unrelated Patient Name");
        var (staffClient, staffId) = await _client.AsStaffAsync();
        await staffClient.PostAsync($"/api/threads/{thread.Id}/assign", JsonContent.Create(new { }));

        var response = await staffClient.GetAsync($"/api/threads?search={CustomWebApplicationFactory.StaffUsername}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ThreadDto>>();

        Assert.NotNull(result);
        Assert.Contains(result!.Items, t => t.Id == thread.Id && t.AssignedStaffId == staffId);
    }

    private async Task<ConversationThread> CreateThreadAsync(string phone, bool patientOptedOut = false, int initialUnreadCount = 0, string? patientName = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();

        var patient = new Patient
        {
            Name = patientName ?? $"Test Patient {phone}",
            Phone = phone,
            OptOutAt = patientOptedOut ? DateTime.UtcNow : null,
        };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var thread = new ConversationThread { PatientId = patient.Id, UnreadCount = initialUnreadCount };
        db.Threads.Add(thread);
        await db.SaveChangesAsync();

        return thread;
    }
}
