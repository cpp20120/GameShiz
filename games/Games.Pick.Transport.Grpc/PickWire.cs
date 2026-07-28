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
