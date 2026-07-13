using System.Text.Json;
using Games.Leaderboard.Contracts;
using Games.Leaderboard.Domain.Models;
using Games.Leaderboard.Domain.Results;
using Games.Leaderboard.Transport.Grpc.Wire;
using Grpc.Core;

namespace Games.Leaderboard.Transport.Grpc;
internal static class LeaderboardWire
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    public static ContractCall Call<T>(T x) => new() { PayloadJson = JsonSerializer.Serialize(x, Options) };
    public static ContractReply Reply<T>(T x) => new() { PayloadJson = JsonSerializer.Serialize(x, Options) };
    public static T Read<T>(this ContractCall x) => JsonSerializer.Deserialize<T>(x.PayloadJson, Options)!;
    public static T Read<T>(this ContractReply x) => JsonSerializer.Deserialize<T>(x.PayloadJson, Options)!;
}
internal sealed record TopCall(int Limit, long ScopeId);
internal sealed record BalanceCall(long UserId, long ScopeId, string DisplayName);
internal sealed record LimitCall(int Limit);

public sealed class LeaderboardGrpcEndpoint(ILeaderboardClient client) : LeaderboardApi.LeaderboardApiBase
{
    public override async Task<ContractReply> GetTop(ContractCall request, ServerCallContext context) { var x = request.Read<TopCall>(); return LeaderboardWire.Reply(await client.GetTopAsync(x.Limit, x.ScopeId, context.CancellationToken)); }
    public override async Task<ContractReply> GetBalance(ContractCall request, ServerCallContext context) { var x = request.Read<BalanceCall>(); return LeaderboardWire.Reply(await client.GetBalanceAsync(x.UserId, x.ScopeId, x.DisplayName, context.CancellationToken)); }
    public override async Task<ContractReply> GetGlobalTop(ContractCall request, ServerCallContext context) { var x = request.Read<LimitCall>(); return LeaderboardWire.Reply(await client.GetGlobalTopAsync(x.Limit, context.CancellationToken)); }
    public override async Task<ContractReply> GetTopByChat(ContractCall request, ServerCallContext context) { var x = request.Read<LimitCall>(); return LeaderboardWire.Reply(await client.GetTopByChatAsync(x.Limit, context.CancellationToken)); }
    public override async Task<ContractReply> ClaimDaily(ContractCall request, ServerCallContext context) { var x = request.Read<BalanceCall>(); return LeaderboardWire.Reply(await client.ClaimDailyAsync(x.UserId, x.ScopeId, x.DisplayName, context.CancellationToken)); }
}

public sealed class GrpcLeaderboardClient(LeaderboardApi.LeaderboardApiClient client) : ILeaderboardClient
{
    public async Task<global::Games.Leaderboard.Domain.Models.Leaderboard> GetTopAsync(int limit, long balanceScopeId, CancellationToken ct) => (await client.GetTopAsync(LeaderboardWire.Call(new TopCall(limit, balanceScopeId)), cancellationToken: ct)).Read<global::Games.Leaderboard.Domain.Models.Leaderboard>();
    public async Task<BalanceInfo> GetBalanceAsync(long userId, long balanceScopeId, string displayName, CancellationToken ct) => (await client.GetBalanceAsync(LeaderboardWire.Call(new BalanceCall(userId, balanceScopeId, displayName)), cancellationToken: ct)).Read<BalanceInfo>();
    public async Task<GlobalLeaderboard> GetGlobalTopAsync(int limit, CancellationToken ct) => (await client.GetGlobalTopAsync(LeaderboardWire.Call(new LimitCall(limit)), cancellationToken: ct)).Read<GlobalLeaderboard>();
    public async Task<MultiChatLeaderboard> GetTopByChatAsync(int perChatLimit, CancellationToken ct) => (await client.GetTopByChatAsync(LeaderboardWire.Call(new LimitCall(perChatLimit)), cancellationToken: ct)).Read<MultiChatLeaderboard>();
    public async Task<DailyClaimResponse> ClaimDailyAsync(long userId, long balanceScopeId, string displayName, CancellationToken ct) => (await client.ClaimDailyAsync(LeaderboardWire.Call(new BalanceCall(userId, balanceScopeId, displayName)), cancellationToken: ct)).Read<DailyClaimResponse>();
}
