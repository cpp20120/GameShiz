using System.Text.Json;
using Games.Redeem.Contracts;
using Games.Redeem.Transport.Grpc.Wire;
using Grpc.Core;

namespace Games.Redeem.Transport.Grpc;

internal sealed record IssueCall(long UserId, string? FreeSpinGameId);
