using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace NotifyHub.Integration.Tests;

/// Boots the real Api pipeline against a real MySQL database — Program.cs's own
/// UseMySql(...) registration and startup MigrateAsync()/seed steps run unmodified,
/// so this is the one factory that exercises genuine relational unique-constraint/
/// locking behavior (needed by InboundWebhookThreadRaceMySqlTests for
/// FindOrCreateThreadAsync's catch(DbUpdateException) branch — EF Core InMemory,
/// used by every other integration test via CustomWebApplicationFactory, can't
/// reproduce that interleaving). ConnectionStrings:Default is deliberately left
/// unset here so it resolves from the ambient environment: CI's mysql service
/// container (ConnectionStrings__Default env var, see ci.yml) or, locally,
/// appsettings.Development.json's docker-compose MySQL.
public class MySqlWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string SharedSecret = "mysql-integration-test-webhook-shared-secret";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "mysql-integration-test-secret-key-at-least-32-chars-long",
                ["Jwt:AccessTokenMinutes"] = "30",
                ["Jwt:RefreshTokenDays"] = "7",
                ["Cors:WebOrigin"] = "http://localhost:5173",
                ["Seed:AdminUsername"] = "mysql-it-admin",
                ["Seed:AdminPassword"] = "MySqlItAdmin1!",
                ["Seed:StaffUsername"] = "mysql-it-staff",
                ["Seed:StaffPassword"] = "MySqlItStaff1!",
                // FR-010: cap PerformanceSeedStep here too — this factory runs Program.cs's
                // real seed pipeline unmodified against a live MySQL instance; there's no
                // benefit to inserting the full 50,000-row production default just to boot
                // this race-condition test.
                ["Seed:PerformanceMessageCount"] = "100",
                ["Webhooks:SharedSecret"] = SharedSecret,
                ["MockGateway:FailRatePercent"] = "0",
                ["MockGateway:MinDelayMs"] = "1",
                ["MockGateway:MaxDelayMs"] = "5",
                ["MockGateway:CallbackBaseUrl"] = "http://localhost",
            });
            // Intentionally no ConnectionStrings:Default override, and no
            // services.RemoveAll<DbContextOptions...>() — Program.cs's real
            // UseMySql(...) registration and startup MigrateAsync()/seed steps
            // run unmodified against a real MySQL instance.
        });
    }
}
