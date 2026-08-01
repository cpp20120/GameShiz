using System.Globalization;
using System.Text.Json;
using BotFramework.Host.Execution;
using Games.Blackjack.Application.Execution;

namespace Games.Blackjack.Infrastructure.Persistence;

public sealed class BlackjackStateReader(IGameAggregateStateReader aggregateStates) : IBlackjackStateReader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<BlackjackGameState?> LoadAsync(long userId, CancellationToken ct)
    {
        var aggregateId = userId.ToString(CultureInfo.InvariantCulture);
        var json = await aggregateStates.LoadJsonAsync("blackjack", aggregateId, ct);
        return json is null
            ? null
            : JsonSerializer.Deserialize<BlackjackGameState>(json, JsonOptions)
                ?? throw new InvalidOperationException("Stored blackjack state is null.");
    }
}
