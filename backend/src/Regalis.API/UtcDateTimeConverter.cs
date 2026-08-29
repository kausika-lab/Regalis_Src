using System.Text.Json;
using System.Text.Json.Serialization;

namespace Regalis.API;

/// <summary>
/// Every DateTime in this system is UTC (A13), but values materialized from SQL
/// Server carry DateTimeKind.Unspecified and would serialize without the Z suffix,
/// which browsers then parse as local time. This converter stamps Unspecified as
/// UTC on the way out and normalizes inbound values to UTC.
/// </summary>
public class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = reader.GetDateTime();
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(
            (value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc))
            .ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"));
}
