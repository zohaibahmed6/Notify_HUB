namespace NotifyHub.Integration.Tests;

/// FailRatePercent=0 — deterministic happy-path outcome for the mock gateway.
public class ReliableGatewayWebApplicationFactory : CustomWebApplicationFactory
{
    protected override int MockGatewayFailRatePercent => 0;
}
