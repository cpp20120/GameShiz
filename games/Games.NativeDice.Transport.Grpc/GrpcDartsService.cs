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

public sealed class GrpcDartsService(GrpcClientFactory factory) : IDartsService
{
    private NativeDiceApi.NativeDiceApiClient Client => factory.CreateClient<NativeDiceApi.NativeDiceApiClient>(NativeDiceGrpcClientNames.Darts);

    public async Task<DartsBetResult> PlaceBetAsync(long userId, string displayName, long chatId, int amount, int replyToMessageId, CancellationToken ct) =>
        (await Client.DartsPlaceBetAsync(NativeDiceWireCodec.Call(new BetCall(userId, displayName, chatId, amount, replyToMessageId)), cancellationToken: ct)).Read<DartsBetResult>()
            with { ClientMustDeliverRoll = true };

    public async Task<DartsThrowResult> ThrowAsync(long roundId, long userId, string displayName, long chatId, int botDiceMessageId, int face, CancellationToken ct) =>
        (await Client.DartsThrowAsync(NativeDiceWireCodec.Call(new DartsThrowCall(roundId, userId, displayName, chatId, botDiceMessageId, face)), cancellationToken: ct)).Read<DartsThrowResult>();

    public async Task<DartsThrowResult> QuickThrowAsync(long userId, string displayName, long chatId, int diceMessageId, int face, int amount, CancellationToken ct) =>
        (await Client.DartsQuickThrowAsync(NativeDiceWireCodec.Call(new DartsThrowCall(0, userId, displayName, chatId, diceMessageId, face, amount)), cancellationToken: ct)).Read<DartsThrowResult>();

    public async Task AbortQueuedRoundIfBetReplyFailedAsync(long roundId, long userId, long chatId, CancellationToken ct) =>
        _ = await Client.DartsAbortAsync(NativeDiceWireCodec.Call(new DartsAbortCall(roundId, userId, chatId)), cancellationToken: ct);
}
