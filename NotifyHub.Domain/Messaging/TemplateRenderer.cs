using System.Text.RegularExpressions;

namespace NotifyHub.Domain.Messaging;

/// Renders {{field}} merge syntax (FR-001) against a set of resolved field values.
/// Server-side only, at send time (BR-013) — the caller is responsible for gathering
/// the field values (patient name, appointment time, etc.) before calling this.
public static partial class TemplateRenderer
{
    [GeneratedRegex(@"\{\{\s*(\w+)\s*\}\}")]
    private static partial Regex FieldPattern();

    /// Unresolved fields are left as-is (e.g. "{{unknown_field}}") rather than throwing,
    /// so a template referencing a field the caller didn't supply degrades visibly instead
    /// of failing the whole send.
    public static string Render(string body, IReadOnlyDictionary<string, string> fields)
    {
        return FieldPattern().Replace(body, match =>
        {
            var fieldName = match.Groups[1].Value;
            return fields.TryGetValue(fieldName, out var value) ? value : match.Value;
        });
    }
}
