using System.Text.Json;
using Games.Redeem.Contracts;
using Games.Redeem.Transport.Grpc.Wire;
using Grpc.Core;

namespace Games.Redeem.Transport.Grpc;

internal static class RedeemWire
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    public static ContractCall Call<T>(T value) => new() { PayloadJson = JsonSerializer.Serialize(value, Options) };
    public static ContractReply Reply<T>(T value) => new() { PayloadJson = JsonSerializer.Serialize(value, Options) };
    public static T Read<T>(this ContractCall value) => JsonSerializer.Deserialize<T>(value.PayloadJson, Options)!;
    public static T Read<T>(this ContractReply value) => JsonSerializer.Deserialize<T>(value.PayloadJson, Options)!;
}
