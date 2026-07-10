namespace NotifyHub.Api.Gateway;

public class MockGatewayOptions
{
    public const string SectionName = "MockGateway";

    public int FailRatePercent { get; set; }
    public int MinDelayMs { get; set; }
    public int MaxDelayMs { get; set; }

    /// Where this Api instance calls itself back to post the delivery receipt
    /// (§: FR-002 "posts receipt via webhook"). Same process, always loopback.
    public string CallbackBaseUrl { get; set; } = "http://localhost:5000";
}
