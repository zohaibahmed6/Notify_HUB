using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Infrastructure.Messaging;
using NotifyHub.Infrastructure.Persistence;
using NotifyHub.Infrastructure.Settings;
using Xunit;

namespace NotifyHub.Integration.Tests;

/// BR-001a: opt-out is checked immediately before calling the gateway, not only at
/// message-creation time — proves a STOP arriving after a message is queued still
/// blocks it. MessageDispatcher isn't hosted inside WebApplicationFactory (Worker-only),
/// so it's instantiated directly here the same way OutboundPipelineTests simulates the
/// dispatcher's claim step, reusing the "self" HttpClient wired to the TestServer.
public class MessageDispatcherOptOutTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task DispatchDueMessagesAsync_BlocksMessage_ForOptedOutPatient()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();

        var patient = new Patient { Name = "Opted Out Patient", Phone = "+19990003001", OptOutAt = DateTime.UtcNow };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var message = new OutboundMessage
        {
            PatientId = patient.Id,
            TemplateId = null,
            SenderType = SenderType.Staff,
            // Backdated so this sorts first in DispatchDueMessagesAsync's CreatedAt-ordered
            // batch (BatchSize=10) ahead of DemoOutboundMessageSeedStep's 10 seeded queued
            // messages, guaranteeing it's included in a single dispatch call.
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            Status = MessageStatus.Queued,
            AttemptCount = 0,
        };
        db.OutboundMessages.Add(message);
        await db.SaveChangesAsync();

        var gatewayClient = factory.Services.GetRequiredService<IHttpClientFactory>().CreateClient("self");
        var dispatcher = new MessageDispatcher(db, gatewayClient, NullLogger<MessageDispatcher>.Instance, new SettingsService(db));

        await dispatcher.DispatchDueMessagesAsync(CancellationToken.None);

        var updated = await db.OutboundMessages.SingleAsync(m => m.Id == message.Id);
        Assert.Equal(MessageStatus.Failed, updated.Status);
        Assert.Equal(0, updated.AttemptCount); // blocked by policy, not a delivery failure
        Assert.Null(updated.NextRetryAt);

        var audit = await db.AuditLogs.SingleAsync(a => a.EntityType == "OutboundMessage" && a.EntityId == message.Id && a.Action == "blocked");
        Assert.Equal("system", audit.Actor);
    }

    [Fact]
    public async Task DispatchDueMessagesAsync_PreservesCommittedRenderedBody_ForTemplateLinkedReminder()
    {
        // Rule 31 reversal (Reminder SMS dialog is now freely editable, edited text
        // committed as RenderedBody at creation — ThreadsController.CreateReminder): the
        // dispatcher must not clobber that committed text by re-rendering from the live
        // template, the way it still does for TemplateId-linked messages with a null
        // RenderedBody (the original, still-default behavior for other TemplateId-linked
        // messages, e.g. DemoOutboundMessageSeedStep's rows).
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();

        var patient = new Patient { Name = "Committed Reminder Patient", Phone = "+19990003003" };
        db.Patients.Add(patient);

        var template = new MessageTemplate
        {
            Name = "Committed reminder test template",
            Body = "This is the CURRENT live template body — should NOT appear in RenderedBody.",
            OffsetHours = 24,
        };
        db.MessageTemplates.Add(template);
        await db.SaveChangesAsync();

        const string committedBody = "Hi, your appointment is on Jul 20, 2026, 3:00 PM.";
        var message = new OutboundMessage
        {
            PatientId = patient.Id,
            TemplateId = template.Id,
            SenderType = SenderType.Staff,
            RenderedBody = committedBody,
            EventTime = DateTime.UtcNow.AddDays(2),
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            Status = MessageStatus.Queued,
            AttemptCount = 0,
        };
        db.OutboundMessages.Add(message);
        await db.SaveChangesAsync();

        var gatewayClient = factory.Services.GetRequiredService<IHttpClientFactory>().CreateClient("self");
        var dispatcher = new MessageDispatcher(db, gatewayClient, NullLogger<MessageDispatcher>.Instance, new SettingsService(db));

        await dispatcher.DispatchDueMessagesAsync(CancellationToken.None);

        var updated = await db.OutboundMessages.SingleAsync(m => m.Id == message.Id);
        Assert.Equal(committedBody, updated.RenderedBody);
    }

    [Fact]
    public async Task DispatchDueMessagesAsync_SkipsBatch_DuringQuietHours()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
        var settingsService = new SettingsService(db);

        // A window that always contains "now", regardless of when the test runs.
        var nowUtc = TimeOnly.FromDateTime(DateTime.UtcNow);
        await settingsService.SetAsync(new Dictionary<string, string>
        {
            [SettingsService.QuietHoursEnabledKey] = "true",
            [SettingsService.QuietHoursStartKey] = nowUtc.AddMinutes(-1).ToString("HH:mm"),
            [SettingsService.QuietHoursEndKey] = nowUtc.AddMinutes(1).ToString("HH:mm"),
        }, CancellationToken.None);

        try
        {
            var patient = new Patient { Name = "Quiet Hours Test Patient", Phone = "+19990003002" };
            db.Patients.Add(patient);
            await db.SaveChangesAsync();

            var message = new OutboundMessage
            {
                PatientId = patient.Id,
                TemplateId = null,
                SenderType = SenderType.Staff,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                Status = MessageStatus.Queued,
                AttemptCount = 0,
                RenderedBody = "Should not send during quiet hours",
            };
            db.OutboundMessages.Add(message);
            await db.SaveChangesAsync();

            var gatewayClient = factory.Services.GetRequiredService<IHttpClientFactory>().CreateClient("self");
            var dispatcher = new MessageDispatcher(db, gatewayClient, NullLogger<MessageDispatcher>.Instance, settingsService);

            var dispatchedCount = await dispatcher.DispatchDueMessagesAsync(CancellationToken.None);

            Assert.Equal(0, dispatchedCount);
            var stillQueued = await db.OutboundMessages.SingleAsync(m => m.Id == message.Id);
            Assert.Equal(MessageStatus.Queued, stillQueued.Status);
        }
        finally
        {
            await settingsService.SetAsync(new Dictionary<string, string> { [SettingsService.QuietHoursEnabledKey] = "false" }, CancellationToken.None);
        }
    }
}
