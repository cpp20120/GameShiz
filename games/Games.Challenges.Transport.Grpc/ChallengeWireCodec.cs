using System.Text.Json;
using Games.Challenges.Transport.Grpc.Wire;

namespace Games.Challenges.Transport.Grpc;

internal static class ChallengeWireCodec
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static ContractCall Call<T>(T value) =>
        new() { PayloadJson = JsonSerializer.Serialize(value, Options) };

    public static ContractReply Reply<T>(T value) =>
        new() { PayloadJson = JsonSerializer.Serialize(value, Options) };

    public static T Read<T>(this ContractCall value) =>
        JsonSerializer.Deserialize<T>(value.PayloadJson, Options)!;

    public static T Read<T>(this ContractReply value) =>
        JsonSerializer.Deserialize<T>(value.PayloadJson, Options)!;
}
