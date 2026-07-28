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

public sealed class DartsRestModule : IRestRouteModule
{
    public string ModuleId => "darts";

    public void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapRestGroup(ModuleId);
        group.MapPost("/play", PlayAsync).WithName("DartsPlay").WithSummary("Place and resolve a Darts bet");
        group.MapPost("/bet", BetAsync).WithName("DartsBet").WithSummary("Queue a Darts bet");
        group.MapPost("/rounds/{roundId:long}/throw", ThrowAsync).WithName("DartsThrow").WithSummary("Resolve a queued Darts round");
    }

    private static async Task<IResult> PlayAsync(
        NativeDicePlayRequest request, RestRequestContext context, IDartsService service,
        IOptions<RestFrameworkOptions> options, CancellationToken ct)
    {
        NativeDiceRestSupport.ValidateAmount(request.Amount);
        var face = NativeDiceRestSupport.RandomFace(6);
        var result = await service.QuickThrowAsync(context.UserId, context.DisplayName, NativeDiceRestSupport.Scope(context),
            NativeDiceRestSupport.SourceId(context, options, "play"), face, request.Amount, ct);
        return Results.Ok(new DartsPlayResponse(result, face));
    }

    private static async Task<IResult> BetAsync(
        NativeDicePlayRequest request, RestRequestContext context, IDartsService service,
        IOptions<RestFrameworkOptions> options, CancellationToken ct)
    {
        NativeDiceRestSupport.ValidateAmount(request.Amount);
        var result = await service.PlaceBetAsync(context.UserId, context.DisplayName, NativeDiceRestSupport.Scope(context), request.Amount,
            NativeDiceRestSupport.SourceId(context, options, "bet"), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> ThrowAsync(
        NativeDiceRollRequest request, long roundId, RestRequestContext context, IDartsService service,
        IOptions<RestFrameworkOptions> options, CancellationToken ct)
    {
        NativeDiceRestSupport.ValidateFace(request.Face, 6);
        var result = await service.ThrowAsync(roundId, context.UserId, context.DisplayName, NativeDiceRestSupport.Scope(context),
            NativeDiceRestSupport.SourceId(context, options, "throw"), request.Face, ct);
        return Results.Ok(result);
    }
}
