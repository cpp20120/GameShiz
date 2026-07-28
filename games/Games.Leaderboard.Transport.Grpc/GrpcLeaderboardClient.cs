using System.Text.Json;
using Games.Leaderboard.Contracts;
using Games.Leaderboard.Domain.Models;
using Games.Leaderboard.Domain.Results;
using Games.Leaderboard.Transport.Grpc.Wire;
using Grpc.Core;

namespace Games.Leaderboard.Transport.Grpc;
public sealed class GrpcLeaderboardClient(LeaderboardApi.LeaderboardApiClient client) : ILeaderboardClient
{
    public async Task<global::Games.Leaderboard.Domain.Models.Leaderboard> GetTopAsync(int limit, long balanceScopeId, CancellationToken ct) => (await client.GetTopAsync(LeaderboardWire.Call(new TopCall(limit, balanceScopeId)), cancellationToken: ct)).Read<global::Games.Leaderboard.Domain.Models.Leaderboard>();
    public async Task<BalanceInfo> GetBalanceAsync(long userId, long balanceScopeId, string displayName, CancellationToken ct) => (await client.GetBalanceAsync(LeaderboardWire.Call(new BalanceCall(userId, balanceScopeId, displayName)), cancellationToken: ct)).Read<BalanceInfo>();
    public async Task<GlobalLeaderboard> GetGlobalTopAsync(int limit, CancellationToken ct) => (await client.GetGlobalTopAsync(LeaderboardWire.Call(new LimitCall(limit)), cancellationToken: ct)).Read<GlobalLeaderboard>();
    public async Task<MultiChatLeaderboard> GetTopByChatAsync(int perChatLimit, CancellationToken ct) => (await client.GetTopByChatAsync(LeaderboardWire.Call(new LimitCall(perChatLimit)), cancellationToken: ct)).Read<MultiChatLeaderboard>();
    public async Task<DailyClaimResponse> ClaimDailyAsync(long userId, long balanceScopeId, string displayName, CancellationToken ct) => (await client.ClaimDailyAsync(LeaderboardWire.Call(new BalanceCall(userId, balanceScopeId, displayName)), cancellationToken: ct)).Read<DailyClaimResponse>();
}
