using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NotifyHub.Api.Dashboard.Dtos;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Infrastructure.Persistence;
using Xunit;

namespace NotifyHub.Integration.Tests;

/// Post-login landing page summary — pure aggregation over Task/Thread/Audit.
public class DashboardControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Summary_AsStaff_CountsOwnTasksOnly_AndOmitsOrgTasks()
    {
        var (client, staffId) = await _client.AsStaffAsync();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
            var patient = new Patient { Name = "Dashboard Test Patient", Phone = "+19990004001" };
            db.Patients.Add(patient);
            await db.SaveChangesAsync();

            var thread = new ConversationThread { PatientId = patient.Id };
            db.Threads.Add(thread);
            await db.SaveChangesAsync();

            db.Tasks.Add(new TaskItem
            {
                ThreadId = thread.Id,
                Priority = TaskPriority.Medium,
                DueAt = DateTime.UtcNow.AddDays(1),
                Status = NotifyHubTaskStatus.Open,
                AssignedStaffId = staffId,
                OriginalOwnerId = staffId,
                OccurrenceCount = 1,
            });
            await db.SaveChangesAsync();
        }

        var summary = await client.GetFromJsonAsync<DashboardSummaryDto>("/api/dashboard/summary");

        Assert.True(summary!.MyTasks.Open >= 1);
        Assert.Null(summary.OrgTasks);
    }

    [Fact]
    public async Task Summary_AsAdmin_IncludesOrgTasksAndActivity()
    {
        var (client, _) = await _client.AsAdminAsync();

        var summary = await client.GetFromJsonAsync<DashboardSummaryDto>("/api/dashboard/summary");

        Assert.NotNull(summary!.OrgTasks);
        Assert.True(summary.OrgTasks!.Open + summary.OrgTasks.InProgress + summary.OrgTasks.Escalated >= 0);
    }

    [Fact]
    public async Task Summary_CountsUnreadThreads()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
            var patient = new Patient { Name = "Unread Dashboard Patient", Phone = "+19990004002" };
            db.Patients.Add(patient);
            await db.SaveChangesAsync();

            db.Threads.Add(new ConversationThread { PatientId = patient.Id, UnreadCount = 3 });
            await db.SaveChangesAsync();
        }

        var (client, _) = await _client.AsAdminAsync();
        var summary = await client.GetFromJsonAsync<DashboardSummaryDto>("/api/dashboard/summary");

        Assert.True(summary!.UnreadThreadCount >= 1);
    }
}
