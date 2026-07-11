using System.Net.Http.Headers;
using System.Net.Http.Json;
using NotifyHub.Api.Auth.Dtos;

namespace NotifyHub.Integration.Tests;

/// Shared login helper for tests that need an authenticated client — logs in and sets
/// the bearer token as the client's default Authorization header. Safe to call per-test
/// since each test method gets its own HttpClient instance (xUnit creates a fresh test
/// class instance per test method).
internal static class TestAuth
{
    public static async Task<(HttpClient Client, long UserId)> AsStaffAsync(this HttpClient client) =>
        await LoginAsync(client, CustomWebApplicationFactory.StaffUsername, CustomWebApplicationFactory.StaffPassword);

    public static async Task<(HttpClient Client, long UserId)> AsAdminAsync(this HttpClient client) =>
        await LoginAsync(client, CustomWebApplicationFactory.AdminUsername, CustomWebApplicationFactory.AdminPassword);

    private static async Task<(HttpClient Client, long UserId)> LoginAsync(HttpClient client, string username, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Username = username, Password = password });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return (client, body.User.Id);
    }
}
