using System.Text.Json;
using Games.Poker.Application.Services;
using Games.Poker.Domain.Entities;
using Games.Poker.Domain.Results;
using Games.Poker.Transport.Grpc.Wire;
using Grpc.Core;
using PokerActionResult = Games.Poker.Domain.Results.ActionResult;

namespace Games.Poker.Transport.Grpc;

internal static class PokerWire
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    public static ContractCall Call<T>(T value) => new() { PayloadJson = JsonSerializer.Serialize(value, Options) };
    public static ContractReply Reply<T>(T value) => new() { PayloadJson = JsonSerializer.Serialize(value, Options) };
    public static T Read<T>(this ContractCall value) => JsonSerializer.Deserialize<T>(value.PayloadJson, Options)!;
    public static T Read<T>(this ContractReply value) => JsonSerializer.Deserialize<T>(value.PayloadJson, Options)!;
}
