using System.Globalization;
using System.Text;
using System.Text.Json;

namespace BotFramework.Host.Admin.Audit;

public static class AdminAuditExportFormatter
{
    public static string ToCsv(IEnumerable<AdminAuditRow> rows)
    {
        var output = new StringBuilder("id,occurred_at,actor_id,actor_name,action,details_json\r\n");
        foreach (var row in rows)
        {
            AppendCsv(output, row.Id.ToString(CultureInfo.InvariantCulture));
            AppendCsv(output, row.OccurredAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            AppendCsv(output, row.ActorId.ToString(CultureInfo.InvariantCulture));
            AppendCsv(output, row.ActorName);
            AppendCsv(output, row.Action);
            AppendCsv(output, row.DetailsJson, last: true);
        }

        return output.ToString();
    }

    public static byte[] ToJson(IEnumerable<AdminAuditRow> rows)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartArray();
            foreach (var row in rows)
            {
                writer.WriteStartObject();
                writer.WriteNumber("id", row.Id);
                writer.WriteString("occurredAt", row.OccurredAt);
                writer.WriteNumber("actorId", row.ActorId);
                writer.WriteString("actorName", row.ActorName);
                writer.WriteString("action", row.Action);
                writer.WritePropertyName("details");
                using var details = JsonDocument.Parse(row.DetailsJson);
                details.RootElement.WriteTo(writer);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
        return stream.ToArray();
    }

    private static void AppendCsv(StringBuilder output, string value, bool last = false)
    {
        var safe = value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r'
            ? "'" + value
            : value;
        output.Append('"').Append(safe.Replace("\"", "\"\"", StringComparison.Ordinal)).Append('"');
        output.Append(last ? "\r\n" : ",");
    }
}
