using Games.Horse.Application.Services;
using Games.Horse.Transport.Grpc.Wire;
using Grpc.Core;

namespace Games.Horse.Transport.Grpc;

public sealed class HorseGrpcEndpoint(IHorseService service) : HorseApi.HorseApiBase
{
    public override async Task<ContractReply> PlaceBet(
        ContractCall request,
        ServerCallContext context)
    {
        var bet = request.Read<BetCall>();
        return HorseWire.Reply(await service.PlaceBetAsync(
            bet.UserId,
            bet.Name,
            bet.ScopeId,
            bet.HorseId,
            bet.Amount,
            bet.SourceId,
            context.CancellationToken));
    }

    public override async Task<ContractReply> GetInfo(
        ContractCall request,
        ServerCallContext context) =>
        HorseWire.Reply(await service.GetTodayInfoAsync(
            request.Read<ScopeCall>().ScopeId,
            context.CancellationToken));

    public override async Task<ContractReply> GetResult(
        ContractCall request,
        ServerCallContext context) =>
        HorseWire.Reply(await service.GetTodayResultAsync(
            request.Read<ScopeCall>().ScopeId ?? 0,
            context.CancellationToken));

    public override async Task<ContractReply> RunRace(
        ContractCall request,
        ServerCallContext context)
    {
        var run = request.Read<RunCall>();
        return HorseWire.Reply(await service.RunRaceAsync(
            run.UserId,
            run.Kind,
            run.ScopeId,
            context.CancellationToken));
    }

    public override async Task<ContractReply> SaveFile(
        ContractCall request,
        ServerCallContext context)
    {
        var file = request.Read<FileCall>();
        await service.SaveFileIdAsync(
            file.RaceDate,
            file.ScopeId,
            file.FileId,
            context.CancellationToken);
        return HorseWire.Reply(EmptyReply.Create());
    }
}
