using System.Text.Json;
using BotFramework.Contracts.Games;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace BotFramework.Host.Games;

/// <summary>Authoritative last-mile guard for split deployments. Local handlers still check the service directly.</summary>
public sealed class GameAvailabilityGrpcInterceptor(IGameAvailabilityService availability) : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request,
        ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var gameId = ResolveGameId(context.Method);
        if (gameId is not null && TryReadChatId(request, out var chatId))
        {
            var state = await availability.GetAsync(chatId, gameId, context.CancellationToken);
            if (!state.Enabled)
                throw new RpcException(new Status(StatusCode.FailedPrecondition,
                    state.Reason is null ? $"Game '{gameId}' is disabled." : $"Game '{gameId}' is disabled: {state.Reason}"));
        }
        return await continuation(request, context);
    }

    private static string? ResolveGameId(string method)
    {
        var value = method.ToLowerInvariant();
        if (value.Contains("nativedice", StringComparison.Ordinal))
        {
            if (value.Contains("darts", StringComparison.Ordinal)) return "darts";
            if (value.Contains("football", StringComparison.Ordinal)) return "football";
            if (value.Contains("basketball", StringComparison.Ordinal)) return "basketball";
            if (value.Contains("bowling", StringComparison.Ordinal)) return "bowling";
            return "dicecube";
        }
        (string Token, string Id)[] games =
        [
            ("dicecube", "dicecube"), ("diceapi", "dice"), ("blackjack", "blackjack"),
            ("challenges", "challenges"), ("horse", "horse"), ("pick", "pick"),
            ("poker", "poker"), ("secrethitler", "sh"), ("transfer", "transfer"),
            ("redeem", "redeem"),
        ];
        return games.FirstOrDefault(game => value.Contains(game.Token, StringComparison.Ordinal)).Id;
    }

    private static bool TryReadChatId<TRequest>(TRequest request, out long chatId)
    {
        chatId = 0;
        var payload = request?.GetType().GetProperty("PayloadJson")?.GetValue(request) as string;
        if (string.IsNullOrWhiteSpace(payload)) return false;
        try
        {
            using var document = JsonDocument.Parse(payload);
            return FindLong(document.RootElement, out chatId);
        }
        catch (JsonException) { return false; }
    }

    private static bool FindLong(JsonElement element, out long value)
    {
        value = 0;
        if (element.ValueKind != JsonValueKind.Object) return false;
        foreach (var property in element.EnumerateObject())
        {
            if ((property.Name.Equals("chatId", StringComparison.OrdinalIgnoreCase)
                 || property.Name.Equals("balanceScopeId", StringComparison.OrdinalIgnoreCase)
                 || property.Name.Equals("scopeId", StringComparison.OrdinalIgnoreCase))
                && property.Value.TryGetInt64(out value)) return true;
        }
        return false;
    }
}
