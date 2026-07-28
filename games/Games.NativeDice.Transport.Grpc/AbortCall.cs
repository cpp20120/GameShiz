using System.Text.Json;
using Games.NativeDice.Transport.Grpc.Wire;

namespace Games.NativeDice.Transport.Grpc;

internal sealed record AbortCall(long UserId, long ChatId, string DisplayName = "", int SourceMessageId = 0);
