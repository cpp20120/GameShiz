using System.Text.Json;
using Games.NativeDice.Transport.Grpc.Wire;

namespace Games.NativeDice.Transport.Grpc;

internal sealed record BetCall(
    long UserId,
    string DisplayName,
    long ChatId,
    int Amount,
    int SourceMessageId);
