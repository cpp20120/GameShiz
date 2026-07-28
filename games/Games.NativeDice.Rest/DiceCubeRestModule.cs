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

public sealed class DiceCubeRestModule : IRestRouteModule
{
    public string ModuleId => "dicecube";

    public void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapRestGroup(ModuleId);
        group.MapPost("/bet", BetAsync).WithName("DiceCubeBet").WithSummary("Place a DiceCube bet");
        group.MapPost("/play", PlayAsync).WithName("DiceCubePlay").WithSummary("Place and resolve a DiceCube bet");
        group.MapPost("/roll", RollAsync).WithName("DiceCubeRoll").WithSummary("Resolve a pending DiceCube bet");
    }

    private static async Task<IResult> BetAsync(
        NativeDicePlayRequest request, RestRequestContext context, IDiceCubeService service,
        IOptions<RestFrameworkOptions> options, CancellationToken ct)
    {
        NativeDiceRestSupport.ValidateAmount(request.Amount);
        var result = await service.PlaceBetAsync(context.UserId, context.DisplayName, NativeDiceRestSupport.Scope(context),
            request.Amount, NativeDiceRestSupport.SourceId(context, options, "bet"), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> PlayAsync(
        NativeDicePlayRequest request, RestRequestContext context, IDiceCubeService service,
        IOptions<RestFrameworkOptions> options, CancellationToken ct)
    {
        NativeDiceRestSupport.ValidateAmount(request.Amount);
        var scope = NativeDiceRestSupport.Scope(context);
        var source = NativeDiceRestSupport.SourceId(context, options, "play");
        var bet = await service.PlaceBetAsync(context.UserId, context.DisplayName, scope, request.Amount, source, ct);
        if (bet.Error != CubeBetError.None)
            return Results.Ok(new DiceCubePlayResponse(bet, null, 0));
        var face = NativeDiceRestSupport.RandomFace(6);
        var roll = await service.RollAsync(context.UserId, context.DisplayName, scope, face, source, ct);
        return Results.Ok(new DiceCubePlayResponse(bet, roll, face));
    }

    private static async Task<IResult> RollAsync(
        NativeDiceRollRequest request, RestRequestContext context, IDiceCubeService service,
        IOptions<RestFrameworkOptions> options, CancellationToken ct)
    {
        NativeDiceRestSupport.ValidateFace(request.Face, 6);
        var result = await service.RollAsync(context.UserId, context.DisplayName, NativeDiceRestSupport.Scope(context), request.Face,
            NativeDiceRestSupport.SourceId(context, options, "roll"), ct);
        return Results.Ok(result);
    }
}
