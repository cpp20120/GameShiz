using System.Text.Json;
using Games.Poker.Application.Services;
using Games.Poker.Domain.Entities;
using Games.Poker.Domain.Results;
using Games.Poker.Transport.Grpc.Wire;
using Grpc.Core;
using PokerActionResult = Games.Poker.Domain.Results.ActionResult;

namespace Games.Poker.Transport.Grpc;

internal sealed record PokerCodeCall(string Code, int MessageId = 0);
