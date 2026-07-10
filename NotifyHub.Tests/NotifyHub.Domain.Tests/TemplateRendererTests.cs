using NotifyHub.Domain.Messaging;
using Xunit;

namespace NotifyHub.Domain.Tests;

public class TemplateRendererTests
{
    [Fact]
    public void Render_SubstitutesKnownFields()
    {
        var result = TemplateRenderer.Render(
            "Hi {{patient_name}}, your appointment is at {{appointment_time}}.",
            new Dictionary<string, string>
            {
                ["patient_name"] = "Jane Doe",
                ["appointment_time"] = "2026-07-12 10:00",
            });

        Assert.Equal("Hi Jane Doe, your appointment is at 2026-07-12 10:00.", result);
    }

    [Fact]
    public void Render_LeavesUnresolvedFieldsAsIs()
    {
        var result = TemplateRenderer.Render(
            "Hi {{patient_name}}, {{unknown_field}}.",
            new Dictionary<string, string> { ["patient_name"] = "Jane Doe" });

        Assert.Equal("Hi Jane Doe, {{unknown_field}}.", result);
    }

    [Fact]
    public void Render_WithNoFields_ReturnsBodyUnchanged()
    {
        var result = TemplateRenderer.Render("No merge fields here.", new Dictionary<string, string>());

        Assert.Equal("No merge fields here.", result);
    }

    [Fact]
    public void Render_ToleratesWhitespaceInsideBraces()
    {
        var result = TemplateRenderer.Render(
            "Hi {{ patient_name }}!",
            new Dictionary<string, string> { ["patient_name"] = "Jane" });

        Assert.Equal("Hi Jane!", result);
    }
}
