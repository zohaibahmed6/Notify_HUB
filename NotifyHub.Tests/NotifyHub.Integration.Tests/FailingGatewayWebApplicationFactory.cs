namespace NotifyHub.Integration.Tests;

/// FailRatePercent=100 — deterministically exercises the retry/backoff path (BR-011).
public class FailingGatewayWebApplicationFactory : CustomWebApplicationFactory
{
    protected override int MockGatewayFailRatePercent => 100;
}
