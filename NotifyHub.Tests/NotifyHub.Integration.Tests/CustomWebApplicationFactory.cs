using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NotifyHub.Infrastructure.Persistence;

namespace NotifyHub.Integration.Tests;

/// Boots the real Api pipeline (routing, auth, controllers) against an EF Core
/// InMemory database instead of MySQL, so most integration tests don't require a
/// live database. For the one test that needs genuine relational unique-constraint/
/// locking behavior (the FindOrCreateThreadAsync race guard), see
/// MySqlWebApplicationFactory / InboundWebhookThreadRaceMySqlTests instead.
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"notifyhub-test-{Guid.NewGuid()}";

    public const string AdminUsername = "integration-admin";
    public const string AdminPassword = "IntegrationAdmin1!";
    public const string StaffUsername = "integration-staff";
    public const string StaffPassword = "IntegrationStaff1!";
    public const string SharedSecret = "integration-test-webhook-shared-secret";

    /// Overridden by subclasses to deterministically drive the mock gateway's outcome —
    /// 0 for happy-path tests, 100 to exercise the retry path (§11: required for the
    /// FR-013 integration test to be reliable, not flaky).
    protected virtual int MockGatewayFailRatePercent => 0;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "unused-in-tests",
                ["Jwt:Secret"] = "integration-test-secret-key-at-least-32-chars-long",
                ["Jwt:AccessTokenMinutes"] = "30",
                ["Jwt:RefreshTokenDays"] = "7",
                ["Cors:WebOrigin"] = "http://localhost:5173",
                ["Seed:AdminUsername"] = AdminUsername,
                ["Seed:AdminPassword"] = AdminPassword,
                ["Seed:StaffUsername"] = StaffUsername,
                ["Seed:StaffPassword"] = StaffPassword,
                // FR-010: keep PerformanceSeedStep tiny here — every InMemory integration test
                // class boots this factory, and the production default (50,000) would make the
                // whole suite seed 50k rows per fixture. PerformanceSeedStepTests exercises the
                // real behavior directly with its own explicit count instead.
                ["Seed:PerformanceMessageCount"] = "50",
                ["Webhooks:SharedSecret"] = SharedSecret,
                ["MockGateway:FailRatePercent"] = MockGatewayFailRatePercent.ToString(),
                ["MockGateway:MinDelayMs"] = "1",
                ["MockGateway:MaxDelayMs"] = "5",
                ["MockGateway:CallbackBaseUrl"] = "http://localhost",
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<NotifyHubDbContext>>();
            services.AddDbContext<NotifyHubDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            // The mock gateway posts its simulated delivery receipt back to this same
            // Api instance (§: FR-002) — routed straight to the TestServer instead of
            // a real socket, so the whole loop stays in-process and deterministic.
            services.AddHttpClient("self").ConfigurePrimaryHttpMessageHandler(() => Server.CreateHandler());
        });
    }
}
