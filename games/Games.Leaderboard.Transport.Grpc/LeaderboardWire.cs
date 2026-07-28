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
