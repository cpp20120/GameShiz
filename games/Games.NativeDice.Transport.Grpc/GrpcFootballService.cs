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

public sealed class GrpcFootballService(GrpcClientFactory factory) : IFootballService
{
    private NativeDiceApi.NativeDiceApiClient Client => factory.CreateClient<NativeDiceApi.NativeDiceApiClient>(NativeDiceGrpcClientNames.Football);

    public Task<FootballBetResult> PlaceBetAsync(long userId, string displayName, long chatId, int amount, CancellationToken ct) =>
        PlaceBetAsync(userId, displayName, chatId, amount, 0, ct);
    public async Task<FootballBetResult> PlaceBetAsync(long userId, string displayName, long chatId, int amount, int sourceMessageId, CancellationToken ct) =>
        (await Client.FootballPlaceBetAsync(NativeDiceWireCodec.Call(new BetCall(userId, displayName, chatId, amount, sourceMessageId)), cancellationToken: ct)).Read<FootballBetResult>();
    public async Task<FootballThrowResult> ThrowAsync(long userId, string displayName, long chatId, int face, CancellationToken ct) =>
        await ThrowAsync(userId, displayName, chatId, face, 0, ct);
    public async Task<FootballThrowResult> ThrowAsync(long userId, string displayName, long chatId, int face, int sourceMessageId, CancellationToken ct) =>
        (await Client.FootballThrowAsync(NativeDiceWireCodec.Call(new RollCall(userId, displayName, chatId, face, sourceMessageId)), cancellationToken: ct)).Read<FootballThrowResult>();
    public async Task AbortPendingBetAfterSendDiceFailedAsync(long userId, long chatId, CancellationToken ct) =>
        _ = await Client.FootballAbortAsync(NativeDiceWireCodec.Call(new AbortCall(userId, chatId)), cancellationToken: ct);
    public async Task AbortPendingBetAfterSendDiceFailedAsync(long userId, string displayName, long chatId, int sourceMessageId, CancellationToken ct) =>
        _ = await Client.FootballAbortAsync(
            NativeDiceWireCodec.Call(new AbortCall(userId, chatId, displayName, sourceMessageId)), cancellationToken: ct);
}
