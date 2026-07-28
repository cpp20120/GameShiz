using System.Text.Json;
using Games.PixelBattle.Contracts;
using Games.PixelBattle.Domain.Entities;
using Games.PixelBattle.Transport.Grpc.Wire;
using Grpc.Core;

namespace Games.PixelBattle.Transport.Grpc;

internal static class PixelBattleWire
{
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    internal static PixelReply Reply<T>(T value) => new() { PayloadJson = JsonSerializer.Serialize(value, Options) };
    internal static T Read<T>(this PixelReply value) => JsonSerializer.Deserialize<T>(value.PayloadJson, Options)!;
}
