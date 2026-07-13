using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Infrastructure.Persistence;
using Xunit;

namespace NotifyHub.Integration.Tests;

/// §7: Inactive/OnLeave users keep read (GET) access but get 403 on any mutating request;
/// login/refresh/logout must keep working regardless of status.
public class ActiveUserRequiredFilterTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task MutatingRequest_ByInactiveUser_Returns403()
    {
        var (client, userId) = await CreateAndLoginInactiveUserAsync("inactive-mutator-9101", "+19990009101");

        var response = await client.PostAsJsonAsync($"/api/threads/{await SeedThreadIdAsync("+19990009101a")}/messages", new { body = "hello" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        GC.KeepAlive(userId);
    }

    [Fact]
    public async Task ReadRequest_ByInactiveUser_StillSucceeds()
    {
        var (client, _) = await CreateAndLoginInactiveUserAsync("inactive-reader-9102", "+19990009102");

        var response = await client.GetAsync("/api/tasks");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_ByInactiveUser_StillSucceeds()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<User>>();

        var user = new User { Username = "inactive-login-9103", Role = UserRole.Staff, Status = UserStatus.Inactive };
        user.PasswordHash = hasher.HashPassword(user, "ValidPass1!");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { username = "inactive-login-9103", password = "ValidPass1!" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<(HttpClient Client, long UserId)> CreateAndLoginInactiveUserAsync(string username, string phoneSeed)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<User>>();

        var user = new User { Username = username, Role = UserRole.Staff, Status = UserStatus.Active };
        user.PasswordHash = hasher.HashPassword(user, "ValidPass1!");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var client = factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { username, password = "ValidPass1!" });
        var body = await loginResponse.Content.ReadFromJsonAsync<NotifyHub.Api.Auth.Dtos.AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", body!.AccessToken);

        // Deactivate after issuing the token, proving the check is DB-status-based, not
        // claims-based (the JWT still says Active — only the DB row changed).
        user.Status = UserStatus.Inactive;
        await db.SaveChangesAsync();

        return (client, user.Id);
    }

    private async Task<long> SeedThreadIdAsync(string phone)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();

        var patient = new Patient { Name = $"Filter Test Patient {phone}", Phone = phone };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var thread = new ConversationThread { PatientId = patient.Id };
        db.Threads.Add(thread);
        await db.SaveChangesAsync();

        return thread.Id;
    }
}
