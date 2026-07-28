using System.Text.Json;
using Games.NativeDice.Transport.Grpc.Wire;

namespace Games.NativeDice.Transport.Grpc;

internal sealed record RollCall(long UserId, string DisplayName, long ChatId, int Face, int SourceMessageId = 0);
