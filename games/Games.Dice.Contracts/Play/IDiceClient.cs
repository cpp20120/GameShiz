using BotFramework.Contracts.Messaging;

namespace Games.Dice.Contracts.Play;

public interface IDiceClient
{
    Task<DicePlayResponse> PlayAsync(
        DicePlayRequest request,
        RequestMetadata metadata,
        CancellationToken ct);
}
