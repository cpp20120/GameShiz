using System.Text.Json;
using Games.PixelBattle.Contracts;
using Games.PixelBattle.Domain.Entities;
using Games.PixelBattle.Transport.Grpc.Wire;
using Grpc.Core;

namespace Games.PixelBattle.Transport.Grpc;

internal sealed record PixelUpdateCall(long UserId, int Index, string Color);
