using Games.Basketball.Application.Services;
using Games.Basketball.Domain.Results;
using Games.Bowling.Application.Services;
using Games.Bowling.Domain.Results;
using Games.Darts.Application.Services;
using Games.Darts.Domain.Results;
using Games.DiceCube.Application.Services;
using Games.DiceCube.Domain.Results;
using Games.Football.Application.Services;
using Games.Football.Domain.Results;
using Games.NativeDice.Transport.Grpc.Wire;
using Grpc.Net.ClientFactory;

namespace Games.NativeDice.Transport.Grpc;

public sealed class GrpcDiceCubeService(GrpcClientFactory factory) : IDiceCubeService
{
    private NativeDiceApi.NativeDiceApiClient Client => factory.CreateClient<NativeDiceApi.NativeDiceApiClient>(NativeDiceGrpcClientNames.DiceCube);

    public Task<CubeBetResult> PlaceBetAsync(long userId, string displayName, long chatId, int amount, CancellationToken ct) =>
        PlaceBetAsync(userId, displayName, chatId, amount, 0, ct);

    public async Task<CubeBetResult> PlaceBetAsync(long userId, string displayName, long chatId, int amount, int sourceMessageId, CancellationToken ct) =>
        (await Client.DiceCubePlaceBetAsync(NativeDiceWireCodec.Call(new BetCall(userId, displayName, chatId, amount, sourceMessageId)), cancellationToken: ct)).Read<CubeBetResult>();

    public async Task<CubeRollResult> RollAsync(long userId, string displayName, long chatId, int face, CancellationToken ct) =>
        (await Client.DiceCubeRollAsync(NativeDiceWireCodec.Call(new RollCall(userId, displayName, chatId, face)), cancellationToken: ct)).Read<CubeRollResult>();

    public async Task<CubeRollResult> RollAsync(long userId, string displayName, long chatId, int face, int sourceMessageId, CancellationToken ct) =>
        (await Client.DiceCubeRollAsync(
            NativeDiceWireCodec.Call(new RollCall(userId, displayName, chatId, face, sourceMessageId)),
            cancellationToken: ct)).Read<CubeRollResult>();

    public async Task AbortPendingBetAfterSendDiceFailedAsync(long userId, long chatId, CancellationToken ct) =>
        _ = await Client.DiceCubeAbortAsync(NativeDiceWireCodec.Call(new AbortCall(userId, chatId)), cancellationToken: ct);

    public async Task AbortPendingBetAfterSendDiceFailedAsync(long userId, string displayName, long chatId, int sourceMessageId,
        CancellationToken ct) =>
        _ = await Client.DiceCubeAbortAsync(
            NativeDiceWireCodec.Call(new AbortCall(userId, chatId, displayName, sourceMessageId)),
            cancellationToken: ct);
}
