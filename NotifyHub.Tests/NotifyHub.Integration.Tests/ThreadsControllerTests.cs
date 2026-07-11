using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NotifyHub.Api.Threads.Dtos;
using NotifyHub.Domain.Entities;
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

    private async Task<ConversationThread> CreateThreadAsync(string phone, bool patientOptedOut = false, int initialUnreadCount = 0)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();

        var patient = new Patient
        {
            Name = $"Test Patient {phone}",
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
