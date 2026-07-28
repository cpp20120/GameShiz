using System.Text.Json;
using Games.NativeDice.Transport.Grpc.Wire;

namespace Games.NativeDice.Transport.Grpc;

internal sealed record DartsThrowCall(
    long RoundId,
    long UserId,
    string DisplayName,
    long ChatId,
    int MessageId,
    int Face,
    int Amount = 0);
