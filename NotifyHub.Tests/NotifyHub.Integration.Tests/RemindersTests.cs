using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NotifyHub.Api.Messages.Dtos;
using NotifyHub.Api.Threads.Dtos;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Infrastructure.Persistence;
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
