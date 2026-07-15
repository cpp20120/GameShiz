using System.Text.Json;
using BotFramework.Contracts.Messaging;
using BotFramework.Contracts.Tenancy;

namespace BotFramework.Sdk.Execution;

public static class AtomicGameSchedule
{
    public const string CommandDataKey = "atomic-command";
    public const string TenantIdDataKey = "__botframework-tenant-id";
    public const string ScopeIdDataKey = "__botframework-scope-id";
    public const string PlayerIdDataKey = "__botframework-player-id";
    public const string RequestIdDataKey = "__botframework-request-id";
    public const string CorrelationIdDataKey = "__botframework-correlation-id";
    public const string ChannelDataKey = "__botframework-channel";

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

    public static IReadOnlyDictionary<string, string> AddTenantContext(
        IReadOnlyDictionary<string, string> data,
        TenantContext context)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(context);
        var result = new Dictionary<string, string>(data, StringComparer.Ordinal)
        {
            [TenantIdDataKey] = context.TenantId.Value,
            [ScopeIdDataKey] = context.ScopeId.Value,
            [RequestIdDataKey] = context.RequestId.Value,
            [CorrelationIdDataKey] = context.CorrelationId.Value,
            [ChannelDataKey] = context.Channel.ToString(),
        };
        if (context.PlayerId is { } player)
            result[PlayerIdDataKey] = player.Value;
        else
            result.Remove(PlayerIdDataKey);
        return result;
    }

    public static bool TryGetTenantContext(
        IReadOnlyDictionary<string, string> data,
        out TenantContext? context)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (!data.TryGetValue(TenantIdDataKey, out var tenantId)
            || !data.TryGetValue(ScopeIdDataKey, out var scopeId)
            || !data.TryGetValue(RequestIdDataKey, out var requestId)
            || !data.TryGetValue(CorrelationIdDataKey, out var correlationId)
            || !data.TryGetValue(ChannelDataKey, out var channelValue)
            || !Enum.TryParse<BotChannel>(channelValue, true, out var channel))
        {
            context = null;
            return false;
        }

        context = TenantContext.Create(
            TenantId.Create(tenantId),
            ScopeId.Create(scopeId),
            data.TryGetValue(PlayerIdDataKey, out var playerId)
                ? PlayerId.Create(playerId)
                : null,
            channel,
            RequestId.Create(requestId),
            RequestId.Create(correlationId));
        return true;
    }
}
