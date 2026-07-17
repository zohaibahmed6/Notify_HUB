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

/// P9-07: the dispatcher's expiry sweep — same direct-instantiation pattern as
/// MessageDispatcherOptOutTests (MessageDispatcher isn't hosted inside
/// WebApplicationFactory, Worker-only).
public class MessageExpiryDispatchTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task DispatchDueMessagesAsync_ExpiresOverdueQueuedMessage_BeforeAttemptingToSend()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();

        var patient = new Patient { Name = "Expiry Test Patient", Phone = "+19990004001" };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var message = new OutboundMessage
        {
            PatientId = patient.Id,
            TemplateId = null,
            SenderType = SenderType.Staff,
            CreatedAt = DateTime.UtcNow.AddHours(-13),
            Status = MessageStatus.Queued,
            AttemptCount = 0,
            RenderedBody = "Should expire, not send",
            ExpiresAt = DateTime.UtcNow.AddHours(-1), // 12h window already passed
        };
        db.OutboundMessages.Add(message);
        await db.SaveChangesAsync();

        var gatewayClient = factory.Services.GetRequiredService<IHttpClientFactory>().CreateClient("self");
        var dispatcher = new MessageDispatcher(db, gatewayClient, NullLogger<MessageDispatcher>.Instance, new SettingsService(db), new MessageBodyRenderer(db));

        await dispatcher.DispatchDueMessagesAsync(CancellationToken.None);

        var updated = await db.OutboundMessages.SingleAsync(m => m.Id == message.Id);
        Assert.Equal(MessageStatus.Expired, updated.Status);
        Assert.Equal(0, updated.AttemptCount); // never attempted — expired before dispatch
        Assert.Equal("Message expired before any send attempt was made.", updated.ExpiryReason);

        var history = await db.DeliveryStatusHistories.SingleAsync(h => h.MessageId == message.Id);
        Assert.Equal(MessageStatus.Expired, history.Status);

        var audit = await db.AuditLogs.SingleAsync(a => a.EntityType == "OutboundMessage" && a.EntityId == message.Id && a.Action == "expired");
        Assert.Equal("system", audit.Actor);
    }

    [Fact]
    public async Task DispatchDueMessagesAsync_StillExpiresOverdueMessage_DuringQuietHours()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
        var settingsService = new SettingsService(db);

        // A window that always contains "now" — same technique as
        // MessageDispatcherOptOutTests.DispatchDueMessagesAsync_SkipsBatch_DuringQuietHours.
        var nowUtc = TimeOnly.FromDateTime(DateTime.UtcNow);
        await settingsService.SetAsync(new Dictionary<string, string>
        {
            [SettingsService.QuietHoursEnabledKey] = "true",
            [SettingsService.QuietHoursStartKey] = nowUtc.AddMinutes(-1).ToString("HH:mm"),
            [SettingsService.QuietHoursEndKey] = nowUtc.AddMinutes(1).ToString("HH:mm"),
        }, CancellationToken.None);

        try
        {
            var patient = new Patient { Name = "Expiry Quiet Hours Test Patient", Phone = "+19990004002" };
            db.Patients.Add(patient);
            await db.SaveChangesAsync();

            var message = new OutboundMessage
            {
                PatientId = patient.Id,
                TemplateId = null,
                SenderType = SenderType.Staff,
                CreatedAt = DateTime.UtcNow.AddHours(-13),
                Status = MessageStatus.Queued,
                AttemptCount = 2,
                RenderedBody = "Should expire even though Quiet Hours gates the batch",
                ExpiresAt = DateTime.UtcNow.AddHours(-1),
            };
            db.OutboundMessages.Add(message);
            await db.SaveChangesAsync();

            var gatewayClient = factory.Services.GetRequiredService<IHttpClientFactory>().CreateClient("self");
            var dispatcher = new MessageDispatcher(db, gatewayClient, NullLogger<MessageDispatcher>.Instance, settingsService, new MessageBodyRenderer(db));

            // Batch itself is suppressed by Quiet Hours (0 dispatched)...
            var dispatchedCount = await dispatcher.DispatchDueMessagesAsync(CancellationToken.None);
            Assert.Equal(0, dispatchedCount);

            // ...but expiry — checked before the Quiet Hours gate — still ran.
            var updated = await db.OutboundMessages.SingleAsync(m => m.Id == message.Id);
            Assert.Equal(MessageStatus.Expired, updated.Status);
            Assert.Equal("Message expired after 2 send attempt(s).", updated.ExpiryReason);
        }
        finally
        {
            await settingsService.SetAsync(new Dictionary<string, string> { [SettingsService.QuietHoursEnabledKey] = "false" }, CancellationToken.None);
        }
    }
}
