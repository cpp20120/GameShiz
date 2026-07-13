using System.Text.Json;

namespace BotFramework.Sdk.Execution;

public static class AtomicGameSchedule
{
    public const string CommandDataKey = "atomic-command";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string JobKey<TCommand>()
    {
        var type = typeof(TCommand);
        return $"atomic-game:{type.Assembly.GetName().Name}:{type.FullName ?? type.Name}";
    }

    public static IReadOnlyDictionary<string, string> SerializeCommand<TCommand>(TCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CommandDataKey] = JsonSerializer.Serialize(command, JsonOptions),
        };
    }

    public static TCommand DeserializeCommand<TCommand>(IReadOnlyDictionary<string, string> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (!data.TryGetValue(CommandDataKey, out var json) || string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("Scheduled atomic command payload is missing.");
        return JsonSerializer.Deserialize<TCommand>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Scheduled atomic command '{typeof(TCommand).Name}' is null.");
    }
}
