using System.Text.Json;

namespace BotFramework.Host.Workflows;

internal static class DurableWorkflowCommandDeserializer
{
    public static object Deserialize(string commandType, string json)
    {
        var separator = commandType.IndexOf(':');
        if (separator <= 0 || separator == commandType.Length - 1)
            throw new InvalidOperationException($"Workflow command type '{commandType}' has an invalid stable name.");
        var assemblyName = commandType[..separator];
        var typeName = commandType[(separator + 1)..];
        var assembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(candidate => string.Equals(candidate.GetName().Name, assemblyName, StringComparison.Ordinal));
        var type = assembly?.GetType(typeName, throwOnError: false, ignoreCase: false)
            ?? throw new InvalidOperationException($"Workflow command type '{commandType}' is not available.");
        if (!typeof(IDurableWorkflowCommand).IsAssignableFrom(type))
            throw new InvalidOperationException($"Workflow command type '{commandType}' is not replayable.");
        return JsonSerializer.Deserialize(json, type, DurableWorkflowJson.Options)
            ?? throw new InvalidOperationException($"Workflow command '{commandType}' could not be deserialized.");
    }
}
