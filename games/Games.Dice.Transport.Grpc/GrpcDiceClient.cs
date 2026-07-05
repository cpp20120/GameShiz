using BotFramework.Contracts.Messaging;
using Games.Dice.Contracts.Play;
using Games.Dice.Transport.Grpc.Wire;

namespace Games.Dice.Transport.Grpc;

public sealed class GrpcDiceClient(DiceApi.DiceApiClient client) : IDiceClient
{
    public async Task<DicePlayResponse> PlayAsync(
        DicePlayRequest request,
        RequestMetadata metadata,
        CancellationToken ct)
    {
        var response = await client.PlayAsync(request.ToGrpc(metadata), cancellationToken: ct);
        return response.ToContract();
    }
}
