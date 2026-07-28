using System.Text.Json;using Games.Horse.Application.Services;using Games.Horse.Domain.Results;using Games.Horse.Transport.Grpc.Wire;using Grpc.Core;
namespace Games.Horse.Transport.Grpc;
internal sealed record RunCall(long UserId,HorseRunKind Kind,long ScopeId);
