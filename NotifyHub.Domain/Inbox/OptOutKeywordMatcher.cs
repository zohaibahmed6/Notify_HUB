namespace NotifyHub.Domain.Inbox;

/// FR-006: matches STOP-keyword variants case-insensitively. The whole inbound body must
/// be (trimmed) one of the keywords — a body like "please stop calling" is a normal reply,
/// not an opt-out request, so this deliberately does not substring-match.
public static class OptOutKeywordMatcher
{
    private static readonly string[] Keywords = ["STOP", "UNSUBSCRIBE", "CANCEL", "END", "QUIT"];

    public static bool IsOptOutRequest(string body) =>
        Keywords.Contains(body.Trim(), StringComparer.OrdinalIgnoreCase);
}
