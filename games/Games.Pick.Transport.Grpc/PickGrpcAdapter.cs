using System.Text.Json;
using Games.Pick.Application.Services;
using Games.Pick.Domain.Results;
using Games.Pick.Infrastructure.Persistence;
using Games.Pick.Application.Results;
using Games.Pick.Application.Analytics;
using Games.Pick.Transport.Grpc.Wire;
using Grpc.Core;

namespace Games.Pick.Transport.Grpc;
internal static class PickWire
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    public static ContractCall Call<T>(T x) => new() { PayloadJson = JsonSerializer.Serialize(x, Options) };
    public static ContractReply Reply<T>(T x) => new() { PayloadJson = JsonSerializer.Serialize(x, Options) };
    public static T Read<T>(this ContractCall x) => JsonSerializer.Deserialize<T>(x.PayloadJson, Options)!;
    public static T Read<T>(this ContractReply x) => JsonSerializer.Deserialize<T>(x.PayloadJson, Options)!;
}
internal sealed record PickCall(long UserId, string Name, long ChatId, int Amount, IReadOnlyList<string> Variants, IReadOnlyList<int> Backed);
internal sealed record ChainCall(PickChainState Chain);
internal sealed record ChainIdCall(Guid Id);
internal sealed record UserCall(long UserId, string Name, long ChatId, int Value = 0);
internal sealed record ChatCall(long ChatId, long UserId = 0, int Limit = 0);
internal sealed record EmptyCall;
internal sealed record EmptyReply;

public sealed class PickGrpcEndpoint(IPickClient client) : PickApi.PickApiBase
{
    public override async Task<ContractReply> Pick(ContractCall r, ServerCallContext c) { var x=r.Read<PickCall>(); return PickWire.Reply(await client.PickAsync(x.UserId,x.Name,x.ChatId,x.Amount,x.Variants,x.Backed,c.CancellationToken)); }
    public override async Task<ContractReply> ContinueChain(ContractCall r, ServerCallContext c) => PickWire.Reply(await client.ContinueChainAsync(r.Read<ChainCall>().Chain,c.CancellationToken));
    public override async Task<ContractReply> ClaimChain(ContractCall r, ServerCallContext c) => PickWire.Reply(await client.ClaimChainAsync(r.Read<ChainIdCall>().Id,c.CancellationToken));
    public override async Task<ContractReply> RestoreChain(ContractCall r, ServerCallContext c) { await client.RestoreChainAsync(r.Read<ChainCall>().Chain,c.CancellationToken); return PickWire.Reply(new EmptyReply()); }
    public override async Task<ContractReply> OpenLottery(ContractCall r, ServerCallContext c) { var x=r.Read<UserCall>(); return PickWire.Reply(await client.OpenLotteryAsync(x.UserId,x.Name,x.ChatId,x.Value,c.CancellationToken)); }
    public override async Task<ContractReply> JoinLottery(ContractCall r, ServerCallContext c) { var x=r.Read<UserCall>(); return PickWire.Reply(await client.JoinLotteryAsync(x.UserId,x.Name,x.ChatId,c.CancellationToken)); }
    public override async Task<ContractReply> LotteryInfo(ContractCall r, ServerCallContext c) => PickWire.Reply(await client.LotteryInfoAsync(r.Read<ChatCall>().ChatId,c.CancellationToken));
    public override async Task<ContractReply> CancelLottery(ContractCall r, ServerCallContext c) { var x=r.Read<ChatCall>(); return PickWire.Reply(await client.CancelLotteryAsync(x.UserId,x.ChatId,c.CancellationToken)); }
    public override async Task<ContractReply> BuyDaily(ContractCall r, ServerCallContext c) { var x=r.Read<UserCall>(); return PickWire.Reply(await client.BuyDailyAsync(x.UserId,x.Name,x.ChatId,x.Value,c.CancellationToken)); }
    public override async Task<ContractReply> DailyInfo(ContractCall r, ServerCallContext c) { var x=r.Read<ChatCall>(); return PickWire.Reply(await client.DailyInfoAsync(x.ChatId,x.UserId,c.CancellationToken)); }
    public override async Task<ContractReply> DailyHistory(ContractCall r, ServerCallContext c) { var x=r.Read<ChatCall>(); return PickWire.Reply(await client.DailyHistoryAsync(x.ChatId,x.Limit,c.CancellationToken)); }
    public override async Task<ContractReply> DailySchedule(ContractCall r, ServerCallContext c) => PickWire.Reply(await client.GetDailyScheduleAsync(c.CancellationToken));
}

public sealed class GrpcPickClient(PickApi.PickApiClient client) : IPickClient
{
    public async Task<PickResult> PickAsync(long u,string n,long ch,int a,IReadOnlyList<string> v,IReadOnlyList<int> b,CancellationToken ct)=>(await client.PickAsync(PickWire.Call(new PickCall(u,n,ch,a,v,b)),cancellationToken:ct)).Read<PickResult>();
    public async Task<PickResult> ContinueChainAsync(PickChainState x,CancellationToken ct)=>(await client.ContinueChainAsync(PickWire.Call(new ChainCall(x)),cancellationToken:ct)).Read<PickResult>();
    public async Task<PickChainState?> ClaimChainAsync(Guid id,CancellationToken ct)=>(await client.ClaimChainAsync(PickWire.Call(new ChainIdCall(id)),cancellationToken:ct)).Read<PickChainState?>();
    public async Task RestoreChainAsync(PickChainState x,CancellationToken ct)=>_ = await client.RestoreChainAsync(PickWire.Call(new ChainCall(x)),cancellationToken:ct);
    public async Task<LotteryOpenResult> OpenLotteryAsync(long u,string n,long ch,int s,CancellationToken ct)=>(await client.OpenLotteryAsync(PickWire.Call(new UserCall(u,n,ch,s)),cancellationToken:ct)).Read<LotteryOpenResult>();
    public async Task<LotteryJoinResult> JoinLotteryAsync(long u,string n,long ch,CancellationToken ct)=>(await client.JoinLotteryAsync(PickWire.Call(new UserCall(u,n,ch)),cancellationToken:ct)).Read<LotteryJoinResult>();
    public async Task<LotteryInfoSnapshot?> LotteryInfoAsync(long ch,CancellationToken ct)=>(await client.LotteryInfoAsync(PickWire.Call(new ChatCall(ch)),cancellationToken:ct)).Read<LotteryInfoSnapshot?>();
    public async Task<LotterySettleResult?> CancelLotteryAsync(long u,long ch,CancellationToken ct)=>(await client.CancelLotteryAsync(PickWire.Call(new ChatCall(ch,u)),cancellationToken:ct)).Read<LotterySettleResult?>();
    public async Task<DailyBuyResult> BuyDailyAsync(long u,string n,long ch,int count,CancellationToken ct)=>(await client.BuyDailyAsync(PickWire.Call(new UserCall(u,n,ch,count)),cancellationToken:ct)).Read<DailyBuyResult>();
    public async Task<DailyInfoSnapshot?> DailyInfoAsync(long ch,long u,CancellationToken ct)=>(await client.DailyInfoAsync(PickWire.Call(new ChatCall(ch,u)),cancellationToken:ct)).Read<DailyInfoSnapshot?>();
    public async Task<IReadOnlyList<PickDailyLotteryRow>> DailyHistoryAsync(long ch,int l,CancellationToken ct)=>(await client.DailyHistoryAsync(PickWire.Call(new ChatCall(ch,Limit:l)),cancellationToken:ct)).Read<IReadOnlyList<PickDailyLotteryRow>>();
    public async Task<PickDailySchedule> GetDailyScheduleAsync(CancellationToken ct)=>(await client.DailyScheduleAsync(PickWire.Call(new EmptyCall()),cancellationToken:ct)).Read<PickDailySchedule>();
}
