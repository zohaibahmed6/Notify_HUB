using System.Text.Json;
using System.Text.Json.Serialization;

namespace NotifyHub.Api.Common;

// Defense in depth alongside NotifyHubDbContext's UtcDateTimeConverter (§11a): every DateTime
// materialized through EF is already relabeled Kind=Utc on read, but this guarantees the JSON
// response always carries a 'Z' suffix even for a DateTime built outside that path, so the
// frontend's UTC->local conversion never silently breaks.
public sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetDateTime();

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
