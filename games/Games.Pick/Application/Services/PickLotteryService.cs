using BotFramework.Host.Execution;
using Games.Pick.Application.Execution;
using Microsoft.Extensions.Options;

namespace Games.Pick.Application.Services;

public sealed class PickLotteryService(
    IPickLotteryStore store,
    IAtomicGameExecutor<QuickLotteryOpenCommand,QuickLotteryState,LotteryOpenResult> open,
    IAtomicGameExecutor<QuickLotteryJoinCommand,QuickLotteryState,LotteryJoinResult> join,
    IAtomicGameExecutor<QuickLotterySettleCommand,QuickLotteryState,LotterySettleResult> settle,
    IOptions<PickOptions> options) : IPickLotteryService
{
    private PickLotteryOptions Opts => options.Value.Lottery;
    public Task<LotteryOpenResult> OpenAsync(long u,string n,long ch,int stake,CancellationToken ct)=>OpenAsync(u,n,ch,stake,0,ct);
    public Task<LotteryOpenResult> OpenAsync(long u,string n,long ch,int stake,int source,CancellationToken ct)
    { var o=Opts; var id=source!=0?$"pick:lottery:open:{ch}:{source}:{u}":$"pick:lottery:open:legacy:{Guid.NewGuid():N}"; return open.ExecuteAsync(new(new(u,n,ch,stake,id,o.MinStake,o.MaxStake,o.DurationSeconds)),ct); }
    public Task<LotteryJoinResult> JoinAsync(long u,string n,long ch,CancellationToken ct)=>JoinAsync(u,n,ch,0,ct);
    public Task<LotteryJoinResult> JoinAsync(long u,string n,long ch,int source,CancellationToken ct)
    { var id=source!=0?$"pick:lottery:join:{ch}:{source}:{u}":$"pick:lottery:join:legacy:{Guid.NewGuid():N}"; return join.ExecuteAsync(new(new(u,n,ch,id)),ct); }
    public async Task<LotteryInfoSnapshot?> InfoAsync(long chatId,CancellationToken ct){var row=await store.FindOpenByChatAsync(chatId,ct);if(row is null)return null;var e=await store.ListEntriesAsync(row.Id,ct);return new(row,e.Count,e.Sum(x=>x.StakePaid));}
    public async Task<LotterySettleResult?> CancelByOpenerAsync(long openerId,long chatId,CancellationToken ct){var row=await store.FindOpenByChatAsync(chatId,ct);if(row is null||row.OpenerId!=openerId)return null;return await ExecuteSettle(row,true,ct);}
    public Task<LotterySettleResult> SettleAsync(PickLotteryRow row,CancellationToken ct)=>ExecuteSettle(row,false,ct);
    private async Task<LotterySettleResult> ExecuteSettle(PickLotteryRow row,bool force,CancellationToken ct){var entries=await store.ListEntriesAsync(row.Id,ct);var o=Opts;return await settle.ExecuteAsync(new(new(row,entries,force,$"pick:lottery:{(force?"cancel":"settle")}:{row.Id:N}",o.MinEntrantsToSettle,o.HouseFeePercent)),ct);}
}
