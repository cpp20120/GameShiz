using System.Text.Json;
using Games.SecretHitler.Application.Services;
using Games.SecretHitler.Domain.Entities;
using Games.SecretHitler.Domain.Results;
using Games.SecretHitler.Transport.Grpc.Wire;
using Grpc.Core;

namespace Games.SecretHitler.Transport.Grpc;

internal sealed record ShUserCall(long UserId, int Value = 0);
