using System.Net;
using System.Net.Http.Json;
using NotifyHub.Api.Auth.Dtos;
using Xunit;

namespace NotifyHub.Integration.Tests;

/// §6a: the refresh token now travels only via an httpOnly cookie (never in the JSON
/// body), so these tests rely on HttpClient's default cookie-container behavior
/// (WebApplicationFactory.CreateClient() defaults HandleCookies=true) — the same client
/// instance auto-resends whatever cookie Login/Refresh most recently set. Tests that
/// need to assert *stale*-cookie rejection capture the Set-Cookie value explicitly and
/// resend it via a raw HttpRequestMessage, since the client's own cookie container will
/// have already moved on to the rotated cookie.
public class AuthEndpointTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Login_WithValidAdminCredentials_ReturnsTokens()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = CustomWebApplicationFactory.AdminUsername,
            Password = CustomWebApplicationFactory.AdminPassword,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
        Assert.Equal("Admin", body.User.Role);
        Assert.True(body.AccessTokenExpiresAt > DateTime.UtcNow);

        var setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("notifyhub_refresh=", setCookie);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_WithValidStaffCredentials_ReturnsStaffRole()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = CustomWebApplicationFactory.StaffUsername,
            Password = CustomWebApplicationFactory.StaffPassword,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Equal("Staff", body!.User.Role);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = CustomWebApplicationFactory.AdminUsername,
            Password = "definitely-wrong-password",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownUsername_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = "no-such-user",
            Password = "whatever1!Password",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedRoute_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithValidToken_ReturnsCurrentUser()
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = CustomWebApplicationFactory.AdminUsername,
            Password = CustomWebApplicationFactory.AdminPassword,
        });
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginBody!.AccessToken);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthUserDto>();
        Assert.Equal(CustomWebApplicationFactory.AdminUsername, body!.Username);
        Assert.Equal("Admin", body.Role);
    }

    [Fact]
    public async Task AdminOnlyRoute_WithStaffToken_ReturnsForbidden()
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = CustomWebApplicationFactory.StaffUsername,
            Password = CustomWebApplicationFactory.StaffPassword,
        });
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/admin-only");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginBody!.AccessToken);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminOnlyRoute_WithAdminToken_ReturnsOk()
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = CustomWebApplicationFactory.AdminUsername,
            Password = CustomWebApplicationFactory.AdminPassword,
        });
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/admin-only");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginBody!.AccessToken);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_UsesCookie_AndRotatesIt()
    {
        var client = factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = CustomWebApplicationFactory.AdminUsername,
            Password = CustomWebApplicationFactory.AdminPassword,
        });
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        var originalCookie = Assert.Single(loginResponse.Headers.GetValues("Set-Cookie"));

        // No body needed — the client's cookie container automatically resends the
        // cookie Login just set.
        var refreshResponse = await client.PostAsync("/api/auth/refresh", content: null);
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var refreshBody = await refreshResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotEqual(loginBody!.AccessToken, refreshBody!.AccessToken);

        var rotatedCookie = Assert.Single(refreshResponse.Headers.GetValues("Set-Cookie"));
        Assert.NotEqual(originalCookie, rotatedCookie);

        // Replaying the original (now-rotated) cookie must fail — the client's own
        // cookie container has already moved on, so resend the captured original value
        // explicitly via a raw Cookie header.
        var originalCookieValue = originalCookie[..originalCookie.IndexOf(';')];
        using var replay = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        replay.Headers.Add("Cookie", originalCookieValue);
        var replayResponse = await factory.CreateClient().SendAsync(replay);
        Assert.Equal(HttpStatusCode.Unauthorized, replayResponse.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithNoCookie_ReturnsUnauthorized()
    {
        var response = await factory.CreateClient().PostAsync("/api/auth/refresh", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithGarbageCookie_ReturnsUnauthorized()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", "notifyhub_refresh=not-a-real-token");

        var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesCookie_AndSubsequentRefreshFails()
    {
        var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = CustomWebApplicationFactory.AdminUsername,
            Password = CustomWebApplicationFactory.AdminPassword,
        });

        var logoutResponse = await client.PostAsync("/api/auth/logout", content: null);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var refreshResponse = await client.PostAsync("/api/auth/refresh", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }
}
