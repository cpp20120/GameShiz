using System.Text.Json;

namespace BotFramework.Host.Workflows;

internal static class DurableWorkflowJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}
