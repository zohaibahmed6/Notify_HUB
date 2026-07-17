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

    /// P9-05: dual-safety net #1 — editing a template's body eagerly re-renders RenderedBody
    /// on any already-Queued message linked to it, right away in the same request, instead of
    /// nulling it and deferring the render to dispatch time (net #2, MessageDispatcher, which
    /// remains a defensive backstop only). ScheduledAt is untouched — only content changes.
    [Fact]
    public async Task Update_Body_ReRendersRenderedBody_OnQueuedMessagesLinkedToTemplate()
    {
        var (client, _) = await _client.AsStaffAsync();

        var created = await client.PostAsJsonAsync("/api/templates", new CreateTemplateRequest
        {
            Name = "Propagation-test template",
            Body = "Original body {{patient_name}}",
            OffsetHours = 48,
        });
        var template = await created.Content.ReadFromJsonAsync<TemplateDto>();

        long queuedMessageId, deliveredMessageId;
        var scheduledAt = DateTime.UtcNow.AddHours(5);
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
                ScheduledAt = scheduledAt,
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
            Assert.Equal("Updated body P9-05 Test Patient", queuedMessage.RenderedBody);
            // Content refreshed immediately; send timing is untouched.
            Assert.Equal(scheduledAt, queuedMessage.ScheduledAt);

            var deliveredMessage = await db.OutboundMessages.SingleAsync(m => m.Id == deliveredMessageId);
            Assert.Equal("Already delivered, must not change", deliveredMessage.RenderedBody);
        }
    }

    [Fact]
    public async Task Create_DefaultsToSms_WhenCommunicationModeOmitted()
    {
        var (client, _) = await _client.AsStaffAsync();

        var created = await client.PostAsJsonAsync("/api/templates", new CreateTemplateRequest
        {
            Name = "Default-mode template",
            Body = "Body",
            OffsetHours = 1,
        });
        var dto = await created.Content.ReadFromJsonAsync<TemplateDto>();

        Assert.Equal("Sms", dto!.CommunicationMode);
    }

    [Fact]
    public async Task List_FiltersByCommunicationMode()
    {
        var (client, _) = await _client.AsStaffAsync();

        var smsCreated = await client.PostAsJsonAsync("/api/templates", new CreateTemplateRequest
        {
            Name = "Sms-mode template",
            Body = "Body",
            OffsetHours = 1,
            CommunicationMode = "Sms",
        });
        var smsTemplate = await smsCreated.Content.ReadFromJsonAsync<TemplateDto>();

        var emailCreated = await client.PostAsJsonAsync("/api/templates", new CreateTemplateRequest
        {
            Name = "Email-mode template",
            Body = "Body",
            OffsetHours = 1,
            CommunicationMode = "Email",
        });
        var emailTemplate = await emailCreated.Content.ReadFromJsonAsync<TemplateDto>();

        var smsOnly = await client.GetFromJsonAsync<List<TemplateDto>>("/api/templates?communicationMode=Sms");
        Assert.Contains(smsOnly!, t => t.Id == smsTemplate!.Id);
        Assert.DoesNotContain(smsOnly!, t => t.Id == emailTemplate!.Id);

        var emailOnly = await client.GetFromJsonAsync<List<TemplateDto>>("/api/templates?communicationMode=Email");
        Assert.Contains(emailOnly!, t => t.Id == emailTemplate!.Id);
        Assert.DoesNotContain(emailOnly!, t => t.Id == smsTemplate!.Id);
    }

    [Fact]
    public async Task Update_RejectsInvalidCommunicationMode()
    {
        var (client, _) = await _client.AsStaffAsync();

        var created = await client.PostAsJsonAsync("/api/templates", new CreateTemplateRequest
        {
            Name = "Invalid-mode-test template",
            Body = "Body",
            OffsetHours = 1,
        });
        var original = await created.Content.ReadFromJsonAsync<TemplateDto>();

        var response = await client.PatchAsJsonAsync($"/api/templates/{original!.Id}", new UpdateTemplateRequest
        {
            CommunicationMode = "Fax",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// §5 (this session): BookmarkIds round-trips through create/update with full-replace
    /// semantics (not additive) on PATCH — same convention as TaskForwardingRulesController.
    [Fact]
    public async Task BookmarkIds_RoundTripThroughCreateAndUpdate()
    {
        var (client, _) = await _client.AsStaffAsync();

        long bookmark1Id, bookmark2Id;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
            var bookmark1 = new Bookmark { Label = "Patient Name", Description = "Inserts patient name", InsertText = "{{patient_name}}" };
            var bookmark2 = new Bookmark { Label = "Appointment Time", Description = "Inserts appointment time", InsertText = "{{appointment_time}}" };
            db.Bookmarks.AddRange(bookmark1, bookmark2);
            await db.SaveChangesAsync();
            bookmark1Id = bookmark1.Id;
            bookmark2Id = bookmark2.Id;
        }

        var created = await client.PostAsJsonAsync("/api/templates", new CreateTemplateRequest
        {
            Name = "Bookmark-test template",
            Body = "Hi {{patient_name}}",
            OffsetHours = 1,
            BookmarkIds = new[] { bookmark1Id },
        });
        var template = await created.Content.ReadFromJsonAsync<TemplateDto>();
        Assert.Equal(new[] { bookmark1Id }, template!.BookmarkIds);

        var patched = await client.PatchAsJsonAsync($"/api/templates/{template.Id}", new UpdateTemplateRequest
        {
            BookmarkIds = new[] { bookmark2Id },
        });
        var updated = await patched.Content.ReadFromJsonAsync<TemplateDto>();

        // Full-replace, not additive: bookmark1 is gone, only bookmark2 remains.
        Assert.Equal(new[] { bookmark2Id }, updated!.BookmarkIds);
    }
}
