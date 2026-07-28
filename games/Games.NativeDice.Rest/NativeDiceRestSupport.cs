using System.Security.Cryptography;
using BotFramework.Rest;
using Games.Basketball.Application.Services;
using Games.Basketball.Domain.Results;
using Games.Bowling.Application.Services;
using Games.Bowling.Domain.Results;
using Games.Darts.Application.Services;
using Games.Darts.Domain.Results;
using Games.DiceCube.Application.Services;
using Games.DiceCube.Domain.Results;
using Games.Football.Application.Services;
using Games.Football.Domain.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Games.NativeDice.Rest;

internal static class NativeDiceRestSupport
{
    public static int SourceId(RestRequestContext context, IOptions<RestFrameworkOptions> options, string action)
    {
        var key = options.Value.RequireIdempotencyKeyForCommands
            ? context.RequireIdempotencyKey()
            : context.IdempotencyKey ?? context.RequestId;
        return RestIdempotency.ToStableSourceId($"native-dice:{action}:{context.ScopeId}:{context.UserId}:{key}");
    }

    public static long Scope(RestRequestContext context) =>
        long.TryParse(context.ScopeId, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new RestBadRequestException("scopeId must be a numeric game scope.");

    public static void ValidateAmount(int amount)
    {
        if (amount <= 0)
            throw new RestBadRequestException("Amount must be positive.");
    }

    public static void ValidateFace(int face, int max)
    {
        if (face is < 1 || face > max)
            throw new RestBadRequestException($"Face must be between 1 and {max}.");
    }

    public static int RandomFace(int max) => RandomNumberGenerator.GetInt32(1, max + 1);

}
