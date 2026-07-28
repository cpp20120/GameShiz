using Games.Horse.Application.Services;
using Games.Horse.Domain.Results;
using Games.Horse.Transport.Grpc.Wire;

namespace Games.Horse.Transport.Grpc;

public sealed class GrpcHorseService(HorseApi.HorseApiClient client) : IHorseService
{
    public Task<BetResult> PlaceBetAsync(
        long userId,
        string displayName,
        long balanceScopeId,
        int horseId,
        int amount,
        CancellationToken ct) =>
        PlaceBetAsync(userId, displayName, balanceScopeId, horseId, amount, 0, ct);

    public async Task<BetResult> PlaceBetAsync(
        long userId,
        string displayName,
        long balanceScopeId,
        int horseId,
        int amount,
        int sourceMessageId,
        CancellationToken ct) =>
        (await client.PlaceBetAsync(
            HorseWire.Call(new BetCall(userId, displayName, balanceScopeId, horseId, amount, sourceMessageId)),
            cancellationToken: ct)).Read<BetResult>();

    public async Task<RaceInfo> GetTodayInfoAsync(long? balanceScopeIdOnly, CancellationToken ct) =>
        (await client.GetInfoAsync(
            HorseWire.Call(new ScopeCall(balanceScopeIdOnly)),
            cancellationToken: ct)).Read<RaceInfo>();

    public async Task<TodayRaceResult> GetTodayResultAsync(long viewerBalanceScopeId, CancellationToken ct) =>
        (await client.GetResultAsync(
            HorseWire.Call(new ScopeCall(viewerBalanceScopeId)),
            cancellationToken: ct)).Read<TodayRaceResult>();

    public async Task<RaceOutcome> RunRaceAsync(
        long callerUserId,
        HorseRunKind kind,
        long chatScopeId,
        CancellationToken ct) =>
        (await client.RunRaceAsync(
            HorseWire.Call(new RunCall(callerUserId, kind, chatScopeId)),
            cancellationToken: ct)).Read<RaceOutcome>();

    public async Task SaveFileIdAsync(
        string raceDate,
        long balanceScopeId,
        string fileId,
        CancellationToken ct) =>
        _ = await client.SaveFileAsync(
            HorseWire.Call(new FileCall(raceDate, balanceScopeId, fileId)),
            cancellationToken: ct);
}
