using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NotifyHub.Api.Templates.Dtos;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Infrastructure.Persistence;
using Xunit;

namespace NotifyHub.Integration.Tests;

/// §6b: Templates & reminder rules screen needs edit, not just create — covers the new
/// PATCH /api/templates/{id} endpoint added in step 6.
public class TemplatesControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Update_AppliesOnlyProvidedFields()
    {
        var (client, _) = await _client.AsStaffAsync();

        var created = await client.PostAsJsonAsync("/api/templates", new CreateTemplateRequest
        {
            Name = "Edit-test template",
            Body = "Original body",
            TriggerType = "AppointmentReminder",
            OffsetHours = 48,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var original = await created.Content.ReadFromJsonAsync<TemplateDto>();

        var patched = await client.PatchAsJsonAsync($"/api/templates/{original!.Id}", new UpdateTemplateRequest
        {
            Body = "Updated body",
            OffsetHours = 24,
        });

        Assert.Equal(HttpStatusCode.OK, patched.StatusCode);
        var updated = await patched.Content.ReadFromJsonAsync<TemplateDto>();

        Assert.Equal("Edit-test template", updated!.Name); // untouched field preserved
        Assert.Equal("Updated body", updated.Body);
        Assert.Equal(24, updated.OffsetHours);
        Assert.Equal("AppointmentReminder", updated.TriggerType);
    }

    [Fact]
    public async Task Update_ReturnsNotFound_ForUnknownId()
    {
        var (client, _) = await _client.AsStaffAsync();

        var response = await client.PatchAsJsonAsync("/api/templates/9999999", new UpdateTemplateRequest { Name = "Nope" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_DefaultsToActive()
    {
        var (client, _) = await _client.AsStaffAsync();

        var created = await client.PostAsJsonAsync("/api/templates", new CreateTemplateRequest
        {
            Name = "Default-active template",
            Body = "Body",
            TriggerType = "MedicationAlert",
            OffsetHours = 1,
        });
        var dto = await created.Content.ReadFromJsonAsync<TemplateDto>();

        Assert.True(dto!.IsActive);
    }

    [Fact]
    public async Task Update_SetsIsActive_AndListFilterRespectsIt()
    {
        var (client, _) = await _client.AsStaffAsync();

        var created = await client.PostAsJsonAsync("/api/templates", new CreateTemplateRequest
        {
            Name = "Deactivate-test template",
            Body = "Body",
            TriggerType = "MedicationAlert",
            OffsetHours = 1,
        });
        var original = await created.Content.ReadFromJsonAsync<TemplateDto>();

        var patched = await client.PatchAsJsonAsync($"/api/templates/{original!.Id}", new UpdateTemplateRequest { IsActive = false });
        var updated = await patched.Content.ReadFromJsonAsync<TemplateDto>();
        Assert.False(updated!.IsActive);

        var activeOnly = await client.GetFromJsonAsync<List<TemplateDto>>("/api/templates?isActive=true");
        Assert.DoesNotContain(activeOnly!, t => t.Id == original.Id);

        var inactiveOnly = await client.GetFromJsonAsync<List<TemplateDto>>("/api/templates?isActive=false");
        Assert.Contains(inactiveOnly!, t => t.Id == original.Id);
    }

    [Fact]
    public async Task Update_RejectsInvalidTriggerType()
    {
        var (client, _) = await _client.AsStaffAsync();

        var created = await client.PostAsJsonAsync("/api/templates", new CreateTemplateRequest
        {
            Name = "Invalid-trigger-test template",
            Body = "Body",
            TriggerType = "MedicationAlert",
            OffsetHours = 1,
        });
        var original = await created.Content.ReadFromJsonAsync<TemplateDto>();

        var response = await client.PatchAsJsonAsync($"/api/templates/{original!.Id}", new UpdateTemplateRequest
        {
            TriggerType = "NotARealType",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// P9-05: dual-safety net #1 — editing a template's body nulls RenderedBody on any
    /// already-Queued message linked to it, forcing a fresh render at dispatch time
    /// (net #2, MessageDispatcher) rather than letting stale content go out.
    [Fact]
    public async Task Update_Body_ClearsRenderedBody_OnQueuedMessagesLinkedToTemplate()
    {
        var (client, _) = await _client.AsStaffAsync();

        var created = await client.PostAsJsonAsync("/api/templates", new CreateTemplateRequest
        {
            Name = "Propagation-test template",
            Body = "Original body {{patient_name}}",
            TriggerType = "AppointmentReminder",
            OffsetHours = 48,
        });
        var template = await created.Content.ReadFromJsonAsync<TemplateDto>();

        long queuedMessageId, deliveredMessageId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
            var patient = new Patient { Name = "P9-05 Test Patient", Phone = "+19990000301" };
            db.Patients.Add(patient);
            await db.SaveChangesAsync();

            var queuedMessage = new OutboundMessage
            {
                PatientId = patient.Id,
                TemplateId = template!.Id,
                SenderType = SenderType.System,
                Status = MessageStatus.Queued,
                RenderedBody = "Stale pre-rendered text",
                CreatedAt = DateTime.UtcNow,
                AttemptCount = 0,
            };
            // Also seed a non-Queued message to prove the sweep is Queued-only.
            var deliveredMessage = new OutboundMessage
            {
                PatientId = patient.Id,
                TemplateId = template.Id,
                SenderType = SenderType.System,
                Status = MessageStatus.Delivered,
                RenderedBody = "Already delivered, must not change",
                CreatedAt = DateTime.UtcNow,
                AttemptCount = 0,
            };
            db.OutboundMessages.AddRange(queuedMessage, deliveredMessage);
            await db.SaveChangesAsync();
            queuedMessageId = queuedMessage.Id;
            deliveredMessageId = deliveredMessage.Id;
        }

        var patched = await client.PatchAsJsonAsync($"/api/templates/{template.Id}", new UpdateTemplateRequest
        {
            Body = "Updated body {{patient_name}}",
        });
        Assert.Equal(HttpStatusCode.OK, patched.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
            var queuedMessage = await db.OutboundMessages.SingleAsync(m => m.Id == queuedMessageId);
            Assert.Null(queuedMessage.RenderedBody);

            var deliveredMessage = await db.OutboundMessages.SingleAsync(m => m.Id == deliveredMessageId);
            Assert.Equal("Already delivered, must not change", deliveredMessage.RenderedBody);
        }
    }
}
