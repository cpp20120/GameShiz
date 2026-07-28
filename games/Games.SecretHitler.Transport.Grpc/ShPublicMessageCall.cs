using System.Text.Json;
using Games.SecretHitler.Application.Services;
using Games.SecretHitler.Domain.Entities;
using Games.SecretHitler.Domain.Results;
using Games.SecretHitler.Transport.Grpc.Wire;
using Grpc.Core;

namespace Games.SecretHitler.Transport.Grpc;

internal sealed record ShPublicMessageCall(string Code, int MessageId);
