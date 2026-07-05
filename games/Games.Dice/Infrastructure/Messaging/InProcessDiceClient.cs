using BotFramework.Contracts.Messaging;
using Games.Dice.Contracts.Play;

namespace Games.Dice.Infrastructure.Messaging;

/// <summary>In-process adapter used by the monolith composition.</summary>
public sealed class InProcessDiceClient(IRequestClient requests) : IDiceClient
{
    public Task<DicePlayResponse> PlayAsync(
        DicePlayRequest request,
        RequestMetadata metadata,
        CancellationToken ct) =>
        requests.SendAsync<DicePlayRequest, DicePlayResponse>(request, metadata, ct);
}
