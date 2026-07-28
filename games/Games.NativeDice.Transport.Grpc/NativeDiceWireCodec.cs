using System.Text.Json;
using Games.NativeDice.Transport.Grpc.Wire;

namespace Games.NativeDice.Transport.Grpc;

internal static class NativeDiceWireCodec
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static ContractCall Call<T>(T value) => new()
    {
        PayloadJson = JsonSerializer.Serialize(value, Options),
    };

    public static T Read<T>(this ContractCall call) =>
        JsonSerializer.Deserialize<T>(call.PayloadJson, Options)
        ?? throw new InvalidOperationException($"Missing {typeof(T).Name} request payload.");

    public static T Read<T>(this ContractReply reply) =>
        JsonSerializer.Deserialize<T>(reply.PayloadJson, Options)
        ?? throw new InvalidOperationException($"Missing {typeof(T).Name} response payload.");

    public static ContractReply Reply<T>(T value) => new()
    {
        PayloadJson = JsonSerializer.Serialize(value, Options),
    };

}
