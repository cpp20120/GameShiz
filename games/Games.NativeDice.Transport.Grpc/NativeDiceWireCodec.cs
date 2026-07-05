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

    public static ContractReply Reply<T>(T value) => new()
    {
        PayloadJson = JsonSerializer.Serialize(value, Options),
    };

    public static T Read<T>(this ContractReply reply) =>
        JsonSerializer.Deserialize<T>(reply.PayloadJson, Options)
        ?? throw new InvalidOperationException($"Missing {typeof(T).Name} response payload.");
}

internal sealed record BetCall(
    long UserId,
    string DisplayName,
    long ChatId,
    int Amount,
    int SourceMessageId);

internal sealed record RollCall(long UserId, string DisplayName, long ChatId, int Face);
internal sealed record AbortCall(long UserId, long ChatId);
internal sealed record DartsThrowCall(
    long RoundId,
    long UserId,
    string DisplayName,
    long ChatId,
    int MessageId,
    int Face,
    int Amount = 0);

internal sealed record DartsAbortCall(long RoundId, long UserId, long ChatId);
internal sealed record EmptyReply;
