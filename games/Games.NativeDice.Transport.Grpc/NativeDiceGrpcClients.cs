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

namespace Games.NativeDice.Transport.Grpc;

public sealed class GrpcDiceCubeService(NativeDiceApi.NativeDiceApiClient client) : IDiceCubeService
{
    public Task<CubeBetResult> PlaceBetAsync(long userId, string displayName, long chatId, int amount, CancellationToken ct) =>
        PlaceBetAsync(userId, displayName, chatId, amount, 0, ct);

    public async Task<CubeBetResult> PlaceBetAsync(long userId, string displayName, long chatId, int amount, int sourceMessageId, CancellationToken ct) =>
        (await client.DiceCubePlaceBetAsync(NativeDiceWireCodec.Call(new BetCall(userId, displayName, chatId, amount, sourceMessageId)), cancellationToken: ct)).Read<CubeBetResult>();

    public async Task<CubeRollResult> RollAsync(long userId, string displayName, long chatId, int face, CancellationToken ct) =>
        (await client.DiceCubeRollAsync(NativeDiceWireCodec.Call(new RollCall(userId, displayName, chatId, face)), cancellationToken: ct)).Read<CubeRollResult>();

    public Task<CubeRollResult> RollAsync(long userId, string displayName, long chatId, int face, int sourceMessageId, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public async Task AbortPendingBetAfterSendDiceFailedAsync(long userId, long chatId, CancellationToken ct) =>
        _ = await client.DiceCubeAbortAsync(NativeDiceWireCodec.Call(new AbortCall(userId, chatId)), cancellationToken: ct);

    public Task AbortPendingBetAfterSendDiceFailedAsync(long userId, string displayName, long chatId, int sourceMessageId,
        CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}

public sealed class GrpcDartsService(NativeDiceApi.NativeDiceApiClient client) : IDartsService
{
    public async Task<DartsBetResult> PlaceBetAsync(long userId, string displayName, long chatId, int amount, int replyToMessageId, CancellationToken ct) =>
        (await client.DartsPlaceBetAsync(NativeDiceWireCodec.Call(new BetCall(userId, displayName, chatId, amount, replyToMessageId)), cancellationToken: ct)).Read<DartsBetResult>()
            with { ClientMustDeliverRoll = true };

    public async Task<DartsThrowResult> ThrowAsync(long roundId, long userId, string displayName, long chatId, int botDiceMessageId, int face, CancellationToken ct) =>
        (await client.DartsThrowAsync(NativeDiceWireCodec.Call(new DartsThrowCall(roundId, userId, displayName, chatId, botDiceMessageId, face)), cancellationToken: ct)).Read<DartsThrowResult>();

    public async Task<DartsThrowResult> QuickThrowAsync(long userId, string displayName, long chatId, int diceMessageId, int face, int amount, CancellationToken ct) =>
        (await client.DartsQuickThrowAsync(NativeDiceWireCodec.Call(new DartsThrowCall(0, userId, displayName, chatId, diceMessageId, face, amount)), cancellationToken: ct)).Read<DartsThrowResult>();

    public async Task AbortQueuedRoundIfBetReplyFailedAsync(long roundId, long userId, long chatId, CancellationToken ct) =>
        _ = await client.DartsAbortAsync(NativeDiceWireCodec.Call(new DartsAbortCall(roundId, userId, chatId)), cancellationToken: ct);
}

public sealed class GrpcFootballService(NativeDiceApi.NativeDiceApiClient client) : IFootballService
{
    public Task<FootballBetResult> PlaceBetAsync(long userId, string displayName, long chatId, int amount, CancellationToken ct) =>
        PlaceBetAsync(userId, displayName, chatId, amount, 0, ct);
    public async Task<FootballBetResult> PlaceBetAsync(long userId, string displayName, long chatId, int amount, int sourceMessageId, CancellationToken ct) =>
        (await client.FootballPlaceBetAsync(NativeDiceWireCodec.Call(new BetCall(userId, displayName, chatId, amount, sourceMessageId)), cancellationToken: ct)).Read<FootballBetResult>();
    public async Task<FootballThrowResult> ThrowAsync(long userId, string displayName, long chatId, int face, CancellationToken ct) =>
        await ThrowAsync(userId, displayName, chatId, face, 0, ct);
    public async Task<FootballThrowResult> ThrowAsync(long userId, string displayName, long chatId, int face, int sourceMessageId, CancellationToken ct) =>
        (await client.FootballThrowAsync(NativeDiceWireCodec.Call(new RollCall(userId, displayName, chatId, face, sourceMessageId)), cancellationToken: ct)).Read<FootballThrowResult>();
    public async Task AbortPendingBetAfterSendDiceFailedAsync(long userId, long chatId, CancellationToken ct) =>
        _ = await client.FootballAbortAsync(NativeDiceWireCodec.Call(new AbortCall(userId, chatId)), cancellationToken: ct);
    public async Task AbortPendingBetAfterSendDiceFailedAsync(long userId, string displayName, long chatId, int sourceMessageId, CancellationToken ct) =>
        _ = await client.FootballAbortAsync(
            NativeDiceWireCodec.Call(new AbortCall(userId, chatId, displayName, sourceMessageId)), cancellationToken: ct);
}

public sealed class GrpcBasketballService(NativeDiceApi.NativeDiceApiClient client) : IBasketballService
{
    public Task<BasketballBetResult> PlaceBetAsync(long userId, string displayName, long chatId, int amount, CancellationToken ct) =>
        PlaceBetAsync(userId, displayName, chatId, amount, 0, ct);
    public async Task<BasketballBetResult> PlaceBetAsync(long userId, string displayName, long chatId, int amount, int sourceMessageId, CancellationToken ct) =>
        (await client.BasketballPlaceBetAsync(NativeDiceWireCodec.Call(new BetCall(userId, displayName, chatId, amount, sourceMessageId)), cancellationToken: ct)).Read<BasketballBetResult>();
    public async Task<BasketballThrowResult> ThrowAsync(long userId, string displayName, long chatId, int face, CancellationToken ct) =>
        await ThrowAsync(userId, displayName, chatId, face, 0, ct);
    public async Task<BasketballThrowResult> ThrowAsync(long userId, string displayName, long chatId, int face, int sourceMessageId, CancellationToken ct) =>
        (await client.BasketballThrowAsync(NativeDiceWireCodec.Call(new RollCall(userId, displayName, chatId, face, sourceMessageId)), cancellationToken: ct)).Read<BasketballThrowResult>();
    public async Task AbortPendingBetAfterSendDiceFailedAsync(long userId, long chatId, CancellationToken ct) =>
        _ = await client.BasketballAbortAsync(NativeDiceWireCodec.Call(new AbortCall(userId, chatId)), cancellationToken: ct);
    public async Task AbortPendingBetAfterSendDiceFailedAsync(long userId, string displayName, long chatId, int sourceMessageId, CancellationToken ct) =>
        _ = await client.BasketballAbortAsync(
            NativeDiceWireCodec.Call(new AbortCall(userId, chatId, displayName, sourceMessageId)),
            cancellationToken: ct);
}

public sealed class GrpcBowlingService(NativeDiceApi.NativeDiceApiClient client) : IBowlingService
{
    public async Task<BowlingBetResult> PlaceBetAsync(long userId, string displayName, long chatId, int amount, int sourceMessageId, CancellationToken ct) =>
        (await client.BowlingPlaceBetAsync(NativeDiceWireCodec.Call(new BetCall(userId, displayName, chatId, amount, sourceMessageId)), cancellationToken: ct)).Read<BowlingBetResult>();
    public async Task<BowlingRollResult> RollAsync(long userId, string displayName, long chatId, int face, CancellationToken ct) =>
        await RollAsync(userId, displayName, chatId, face, 0, ct);
    public async Task<BowlingRollResult> RollAsync(long userId, string displayName, long chatId, int face, int sourceMessageId, CancellationToken ct) =>
        (await client.BowlingRollAsync(NativeDiceWireCodec.Call(new RollCall(userId, displayName, chatId, face, sourceMessageId)), cancellationToken: ct)).Read<BowlingRollResult>();
    public async Task AbortPendingBetAfterSendDiceFailedAsync(long userId, long chatId, CancellationToken ct) =>
        _ = await client.BowlingAbortAsync(NativeDiceWireCodec.Call(new AbortCall(userId, chatId)), cancellationToken: ct);
    public async Task AbortPendingBetAfterSendDiceFailedAsync(long userId, string displayName, long chatId, int sourceMessageId, CancellationToken ct) =>
        _ = await client.BowlingAbortAsync(
            NativeDiceWireCodec.Call(new AbortCall(userId, chatId, displayName, sourceMessageId)), cancellationToken: ct);
}
