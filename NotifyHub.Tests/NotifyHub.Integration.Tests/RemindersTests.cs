using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NotifyHub.Api.Messages.Dtos;
using NotifyHub.Api.Threads.Dtos;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Infrastructure.Messaging;
using NotifyHub.Infrastructure.Persistence;
using NotifyHub.Infrastructure.Settings;
using Xunit;

namespace NotifyHub.Integration.Tests;

/// P9-08: Reminder SMS creation (ThreadsController.CreateReminder) + edit/cancel
/// (MessagesController). Each test creates its own patient/thread/template — distinct
/// phone numbers outside every other test class's ranges, same isolation convention as
/// ThreadsControllerTests.
public class RemindersTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CreateReminder_Succeeds_AndComputesScheduledSendTimeAndExpiryFromEventTime()
    {
        var (threadId, templateId) = await SeedThreadAndTemplateAsync("+19990005001");
        var (client, _) = await _client.AsStaffAsync();

        // Default offset 1440 min (24h), default expiry offset 15 min — seeded defaults.
        var eventTime = DateTime.UtcNow.AddDays(2);
        var response = await client.PostAsJsonAsync($"/api/threads/{threadId}/reminders", new CreateReminderRequest
        {
            TemplateId = templateId,
            EventTime = eventTime,
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
        var message = await db.OutboundMessages.SingleAsync(m => m.ThreadId == threadId);

        Assert.Equal(MessageStatus.Queued, message.Status);
        Assert.Equal(templateId, message.TemplateId);
        Assert.Null(message.RenderedBody); // rendered at dispatch, not committed at creation
        Assert.Equal(eventTime, message.EventTime!.Value, TimeSpan.FromSeconds(1));
        Assert.Equal(1440, message.ReminderOffsetMinutes);
        Assert.Equal(15, message.ReminderExpiryOffsetMinutes);
        Assert.Equal(eventTime.AddMinutes(-1440), message.ScheduledAt!.Value, TimeSpan.FromSeconds(1));
        Assert.Equal(eventTime.AddMinutes(-15), message.ExpiresAt!.Value, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CreateReminder_WithBody_CommitsItAsRenderedBody()
    {
        // Rule 31 reversal: the Reminder SMS dialog is now freely editable, and an edited
        // body is committed at creation instead of staying null/rendered-fresh-at-dispatch.
        var (threadId, templateId) = await SeedThreadAndTemplateAsync("+19990005007");
        var (client, _) = await _client.AsStaffAsync();

        var response = await client.PostAsJsonAsync($"/api/threads/{threadId}/reminders", new CreateReminderRequest
        {
            TemplateId = templateId,
            EventTime = DateTime.UtcNow.AddDays(2),
            Body = "Hi Jane, your appointment is on Jul 20, 2026, 3:00 PM.",
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
        var message = await db.OutboundMessages.SingleAsync(m => m.ThreadId == threadId);

        Assert.Equal(templateId, message.TemplateId); // still linked, kept for idempotency/reporting
        Assert.Equal("Hi Jane, your appointment is on Jul 20, 2026, 3:00 PM.", message.RenderedBody);
    }

    [Fact]
    public async Task CreateReminder_AppearsInThreadDetail_WithEventTimeAndScheduledAt()
    {
        // End-to-end proof (not just the entity-level assertions above) that a newly
        // created reminder is actually visible in the inbox: still Queued, and carrying
        // both its Event Time and computed Scheduled Send Time through GET /api/threads/{id}.
        var (threadId, templateId) = await SeedThreadAndTemplateAsync("+19990005009");
        var (client, _) = await _client.AsStaffAsync();
        var eventTime = DateTime.UtcNow.AddDays(2);

        var created = await client.PostAsJsonAsync($"/api/threads/{threadId}/reminders", new CreateReminderRequest
        {
            TemplateId = templateId,
            EventTime = eventTime,
            Body = "Reminder body committed at creation",
        });
        Assert.Equal(HttpStatusCode.NoContent, created.StatusCode);

        var response = await client.GetAsync($"/api/threads/{threadId}");
        var body = await response.Content.ReadFromJsonAsync<ThreadDetailDto>();
        var message = body!.Messages.Items.Single(m => m.Body == "Reminder body committed at creation");

        Assert.Equal("Queued", message.Status);
        Assert.NotNull(message.EventTime);
        Assert.Equal(eventTime, message.EventTime!.Value, TimeSpan.FromSeconds(1));
        Assert.NotNull(message.ScheduledAt);
        Assert.Equal(eventTime.AddMinutes(-1440), message.ScheduledAt!.Value, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CreateReminder_RejectsPastScheduledSendTime()
    {
        var (threadId, templateId) = await SeedThreadAndTemplateAsync("+19990005002");
        var (client, _) = await _client.AsStaffAsync();

        // Default offset is 1440 min (24h) — an Event Time only 1 hour out computes a
        // Scheduled Send Time 23h in the past (rule 8/9).
        var response = await client.PostAsJsonAsync($"/api/threads/{threadId}/reminders", new CreateReminderRequest
        {
            TemplateId = templateId,
            EventTime = DateTime.UtcNow.AddHours(1),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateReminder_RejectsDuplicate_SamePatientTemplateEventTimeOffset()
    {
        var (threadId, templateId) = await SeedThreadAndTemplateAsync("+19990005003");
        var (client, _) = await _client.AsStaffAsync();
        var eventTime = DateTime.UtcNow.AddDays(2);

        var first = await client.PostAsJsonAsync($"/api/threads/{threadId}/reminders", new CreateReminderRequest { TemplateId = templateId, EventTime = eventTime });
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        var duplicate = await client.PostAsJsonAsync($"/api/threads/{threadId}/reminders", new CreateReminderRequest { TemplateId = templateId, EventTime = eventTime });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task UpdateReminder_RecalculatesScheduledSendTimeAndExpiry_FromNewEventTime()
    {
        var (threadId, templateId) = await SeedThreadAndTemplateAsync("+19990005004");
        var (client, _) = await _client.AsStaffAsync();
        var originalEventTime = DateTime.UtcNow.AddDays(2);

        await client.PostAsJsonAsync($"/api/threads/{threadId}/reminders", new CreateReminderRequest { TemplateId = templateId, EventTime = originalEventTime });

        long messageId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
            messageId = (await db.OutboundMessages.SingleAsync(m => m.ThreadId == threadId)).Id;
        }

        var newEventTime = DateTime.UtcNow.AddDays(3);
        var patched = await client.PatchAsJsonAsync($"/api/messages/{messageId}", new UpdateReminderRequest { EventTime = newEventTime });
        Assert.Equal(HttpStatusCode.NoContent, patched.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
            var updated = await db.OutboundMessages.SingleAsync(m => m.Id == messageId);
            Assert.Equal(newEventTime, updated.EventTime!.Value, TimeSpan.FromSeconds(1));
            Assert.Equal(newEventTime.AddMinutes(-1440), updated.ScheduledAt!.Value, TimeSpan.FromSeconds(1));
            Assert.Equal(newEventTime.AddMinutes(-15), updated.ExpiresAt!.Value, TimeSpan.FromSeconds(1));
        }
    }

    [Fact]
    public async Task Cancel_SetsStatusToCancelled_AndBlocksNonQueuedReminder()
    {
        var (threadId, templateId) = await SeedThreadAndTemplateAsync("+19990005005");
        var (client, _) = await _client.AsStaffAsync();

        await client.PostAsJsonAsync($"/api/threads/{threadId}/reminders", new CreateReminderRequest { TemplateId = templateId, EventTime = DateTime.UtcNow.AddDays(2) });

        long messageId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
            messageId = (await db.OutboundMessages.SingleAsync(m => m.ThreadId == threadId)).Id;
        }

        var cancelled = await client.PostAsync($"/api/messages/{messageId}/cancel", null);
        Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
            var updated = await db.OutboundMessages.SingleAsync(m => m.Id == messageId);
            Assert.Equal(MessageStatus.Cancelled, updated.Status);
        }

        // Rule 29: cancelled reminders must never be processed further, including a second cancel.
        var secondCancel = await client.PostAsync($"/api/messages/{messageId}/cancel", null);
        Assert.Equal(HttpStatusCode.BadRequest, secondCancel.StatusCode);
    }

    [Fact]
    public async Task Cancel_RejectsStandardSms_WithNoEventTime()
    {
        var (threadId, _) = await SeedThreadAndTemplateAsync("+19990005006");
        var (client, _) = await _client.AsStaffAsync();

        await client.PostAsJsonAsync($"/api/threads/{threadId}/messages", new ReplyRequest { Body = "Standard SMS, not a reminder" });

        long messageId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
            messageId = (await db.OutboundMessages.SingleAsync(m => m.ThreadId == threadId)).Id;
        }

        var cancelled = await client.PostAsync($"/api/messages/{messageId}/cancel", null);
        Assert.Equal(HttpStatusCode.BadRequest, cancelled.StatusCode);
    }

    [Fact]
    public async Task Dispatch_RendersAppointmentTimeFromEventTime_WhenBodyLeftBlankAtCreation()
    {
        // Closes the gap where a Reminder SMS created with a blank body (RenderedBody left
        // null, deferring rendering to dispatch time) would previously go out with the
        // literal, unresolved "{{appointment_time}}" text — RenderAsync never read
        // message.EventTime, only TriggerReference (which reminders always leave null).
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();

        var gatewayClient = factory.Services.GetRequiredService<IHttpClientFactory>().CreateClient("self");
        var dispatcher = new MessageDispatcher(db, gatewayClient, NullLogger<MessageDispatcher>.Instance, new SettingsService(db));

        // Drain the factory's seeded performance-test backlog first (Seed:PerformanceMessageCount
        // in CustomWebApplicationFactory) — otherwise those older-CreatedAt rows fill the
        // batch (Take(10)) ahead of the message this test creates below, and it never gets
        // dispatched within a single call.
        while (await dispatcher.DispatchDueMessagesAsync(CancellationToken.None) > 0)
        {
        }

        var patient = new Patient { Name = "Dispatch Reminder Test Patient", Phone = "+19990005008" };
        db.Patients.Add(patient);

        var template = new MessageTemplate
        {
            Name = "Dispatch reminder test template",
            Body = "Hi {{patient_name}}, see you at {{appointment_time}}.",
            TriggerType = TriggerType.AppointmentReminder,
            OffsetHours = 24,
        };
        db.MessageTemplates.Add(template);
        await db.SaveChangesAsync();

        var eventTime = DateTime.UtcNow.AddHours(2);
        var message = new OutboundMessage
        {
            PatientId = patient.Id,
            TemplateId = template.Id,
            SenderType = SenderType.Staff,
            TriggerReference = null,
            RenderedBody = null, // blank-body-at-creation path
            CreatedAt = DateTime.UtcNow,
            Status = MessageStatus.Queued,
            AttemptCount = 0,
            ScheduledAt = DateTime.UtcNow.AddMinutes(-1), // already due
            ExpiresAt = DateTime.UtcNow.AddHours(1), // not yet expired
            EventTime = eventTime,
            ReminderOffsetMinutes = 1440,
            ReminderExpiryOffsetMinutes = 15,
        };
        db.OutboundMessages.Add(message);
        await db.SaveChangesAsync();

        await dispatcher.DispatchDueMessagesAsync(CancellationToken.None);

        var updated = await db.OutboundMessages.SingleAsync(m => m.Id == message.Id);
        Assert.Contains(eventTime.ToString("u"), updated.RenderedBody);
        Assert.DoesNotContain("{{appointment_time}}", updated.RenderedBody);
    }

    private async Task<(long ThreadId, long TemplateId)> SeedThreadAndTemplateAsync(string phone)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();

        var patient = new Patient { Name = $"Reminder Test Patient {phone}", Phone = phone };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var thread = new ConversationThread { PatientId = patient.Id, UnreadCount = 0 };
        db.Threads.Add(thread);

        var template = new MessageTemplate
        {
            Name = $"Reminder test template {phone}",
            Body = "Hi {{patient_name}}, this is a reminder.",
            TriggerType = TriggerType.AppointmentReminder,
            OffsetHours = 24,
        };
        db.MessageTemplates.Add(template);

        await db.SaveChangesAsync();

        return (thread.Id, template.Id);
    }
}
