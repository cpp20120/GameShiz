using System.Text.Json;
using Games.Leaderboard.Contracts;
using Games.Leaderboard.Domain.Models;
using Games.Leaderboard.Domain.Results;
using Games.Leaderboard.Transport.Grpc.Wire;
using Grpc.Core;

namespace Games.Leaderboard.Transport.Grpc;
internal sealed record LimitCall(int Limit);
