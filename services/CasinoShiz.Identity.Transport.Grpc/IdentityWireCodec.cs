using System.Text.Json;
using CasinoShiz.Identity.Transport.Grpc.Wire;

namespace CasinoShiz.Identity.Transport.Grpc;

internal static class IdentityWireCodec
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static IdentityCall Call<T>(T value) =>
        new() { PayloadJson = JsonSerializer.Serialize(value, Options) };

    public static IdentityReply Reply<T>(T value) =>
        new() { PayloadJson = JsonSerializer.Serialize(value, Options) };

    public static T Read<T>(this IdentityCall value) =>
        JsonSerializer.Deserialize<T>(value.PayloadJson, Options)!;

    public static T Read<T>(this IdentityReply value) =>
        JsonSerializer.Deserialize<T>(value.PayloadJson, Options)!;
}
