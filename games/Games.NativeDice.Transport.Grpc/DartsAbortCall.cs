using System.Text.Json;
using Games.NativeDice.Transport.Grpc.Wire;

namespace Games.NativeDice.Transport.Grpc;

internal sealed record DartsAbortCall(long RoundId, long UserId, long ChatId);
