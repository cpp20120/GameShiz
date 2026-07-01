using System.Text;
using System.Text.Json;
using BotFramework.Host.Admin.Audit;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class AdminAuditExportFormatterTests
{
    private static readonly AdminAuditRow Row = new(
        7,
        42,
        "=HYPERLINK(\"bad\")",
        "recovery.event_retry",
        "{\"recordId\":123,\"message\":\"<b>stored text</b>\"}",
        new DateTimeOffset(2026, 7, 1, 1, 2, 3, TimeSpan.Zero));

    [Fact]
    public void Csv_EscapesQuotesAndNeutralizesSpreadsheetFormulas()
    {
        var csv = AdminAuditExportFormatter.ToCsv([Row]);

        Assert.Contains("\"'=HYPERLINK(\"\"bad\"\")\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"{\"\"recordId\"\":123", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Json_ExportsDetailsAsStructuredJson()
    {
        using var document = JsonDocument.Parse(AdminAuditExportFormatter.ToJson([Row]));
        var exported = document.RootElement[0];

        Assert.Equal(123, exported.GetProperty("details").GetProperty("recordId").GetInt32());
        Assert.Equal("<b>stored text</b>", exported.GetProperty("details").GetProperty("message").GetString());
        Assert.Equal(Row.ActorName, exported.GetProperty("actorName").GetString());
    }
}
