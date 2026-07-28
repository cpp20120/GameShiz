using System.Text.Json;
using Games.Pick.Application.Services;
using Games.Pick.Domain.Results;
using Games.Pick.Infrastructure.Persistence;
using Games.Pick.Application.Results;
using Games.Pick.Application.Analytics;
using Games.Pick.Transport.Grpc.Wire;
using Grpc.Core;

namespace Games.Pick.Transport.Grpc;
internal sealed record UserCall(long UserId, string Name, long ChatId, int Value = 0, int SourceMessageId = 0);
