using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NotifyHub.Api.Audit.Dtos;
using NotifyHub.Api.Common;
using NotifyHub.Domain.Entities;
using NotifyHub.Infrastructure.Persistence;
using Xunit;

namespace NotifyHub.Integration.Tests;

/// FR-011/§8: GET /api/audit (Admin, all actors) and GET /api/audit/mine (Staff, own actions
/// only). Each test uses a distinct, uniquely-named action/entity type as a marker so
/// assertions aren't affected by audit rows other tests in this shared-DB fixture create.
public class AuditControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task List_AsAdmin_ReturnsRowsAcrossAllActors()
    {
        await SeedAuditRowAsync(actor: "system", action: "audit-test-admin-view", entityId: 9001);
        await SeedAuditRowAsync(actor: CustomWebApplicationFactory.StaffUsername, action: "audit-test-admin-view", entityId: 9002);

        var (client, _) = await _client.AsAdminAsync();
        var response = await client.GetAsync("/api/audit?action=audit-test-admin-view&pageSize=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<AuditLogDto>>();
        Assert.Equal(2, body!.TotalCount);
        Assert.Contains(body.Items, i => i.Actor == "system");
        Assert.Contains(body.Items, i => i.Actor == CustomWebApplicationFactory.StaffUsername);
    }

    [Fact]
    public async Task List_AsStaff_IsForbidden()
    {
        var (client, _) = await _client.AsStaffAsync();

        var response = await client.GetAsync("/api/audit");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Mine_ReturnsOnlyCallersOwnActions()
    {
        await SeedAuditRowAsync(actor: "system", action: "audit-test-mine-view", entityId: 9101);
        await SeedAuditRowAsync(actor: CustomWebApplicationFactory.StaffUsername, action: "audit-test-mine-view", entityId: 9102);

        var (client, _) = await _client.AsStaffAsync();
        var response = await client.GetAsync("/api/audit/mine?action=audit-test-mine-view");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<AuditLogDto>>();
        var item = Assert.Single(body!.Items);
        Assert.Equal(CustomWebApplicationFactory.StaffUsername, item.Actor);
    }

    [Fact]
    public async Task List_FiltersByDateRange()
    {
        var now = DateTime.UtcNow;
        await SeedAuditRowAsync(actor: "system", action: "audit-test-daterange", entityId: 9201, occurredAt: now.AddDays(-10));
        await SeedAuditRowAsync(actor: "system", action: "audit-test-daterange", entityId: 9202, occurredAt: now);

        var (client, _) = await _client.AsAdminAsync();
        var response = await client.GetAsync($"/api/audit?action=audit-test-daterange&from={now.AddDays(-1):o}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<AuditLogDto>>();
        var item = Assert.Single(body!.Items);
        Assert.Equal(9202, item.EntityId);
    }

    private async Task SeedAuditRowAsync(string actor, string action, long entityId, DateTime? occurredAt = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();

        db.AuditLogs.Add(new AuditLog
        {
            Actor = actor,
            Action = action,
            EntityType = "AuditTestMarker",
            EntityId = entityId,
            OccurredAt = occurredAt ?? DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }
}
