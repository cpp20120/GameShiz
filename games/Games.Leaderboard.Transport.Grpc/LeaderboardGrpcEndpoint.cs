using System.Text.Json;
using Games.Leaderboard.Contracts;
using Games.Leaderboard.Domain.Models;
using Games.Leaderboard.Domain.Results;
using Games.Leaderboard.Transport.Grpc.Wire;
using Grpc.Core;

namespace Games.Leaderboard.Transport.Grpc;
public sealed class LeaderboardGrpcEndpoint(ILeaderboardClient client) : LeaderboardApi.LeaderboardApiBase
{
    public override async Task<ContractReply> GetTop(ContractCall request, ServerCallContext context) { var x = request.Read<TopCall>(); return LeaderboardWire.Reply(await client.GetTopAsync(x.Limit, x.ScopeId, context.CancellationToken)); }
    public override async Task<ContractReply> GetBalance(ContractCall request, ServerCallContext context) { var x = request.Read<BalanceCall>(); return LeaderboardWire.Reply(await client.GetBalanceAsync(x.UserId, x.ScopeId, x.DisplayName, context.CancellationToken)); }
    public override async Task<ContractReply> GetGlobalTop(ContractCall request, ServerCallContext context) { var x = request.Read<LimitCall>(); return LeaderboardWire.Reply(await client.GetGlobalTopAsync(x.Limit, context.CancellationToken)); }
    public override async Task<ContractReply> GetTopByChat(ContractCall request, ServerCallContext context) { var x = request.Read<LimitCall>(); return LeaderboardWire.Reply(await client.GetTopByChatAsync(x.Limit, context.CancellationToken)); }
    public override async Task<ContractReply> ClaimDaily(ContractCall request, ServerCallContext context) { var x = request.Read<BalanceCall>(); return LeaderboardWire.Reply(await client.ClaimDailyAsync(x.UserId, x.ScopeId, x.DisplayName, context.CancellationToken)); }
}
