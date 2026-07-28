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

public sealed class BasketballRestModule : IRestRouteModule
{
    public string ModuleId => "basketball";

    public void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapRestGroup(ModuleId);
        group.MapPost("/bet", BetAsync).WithName("BasketballBet").WithSummary("Place a Basketball bet");
        group.MapPost("/play", PlayAsync).WithName("BasketballPlay").WithSummary("Place and resolve a Basketball bet");
        group.MapPost("/roll", RollAsync).WithName("BasketballRoll").WithSummary("Resolve a pending Basketball bet");
    }

    private static async Task<IResult> BetAsync(NativeDicePlayRequest request, RestRequestContext context, IBasketballService service,
        IOptions<RestFrameworkOptions> options, CancellationToken ct)
    {
        NativeDiceRestSupport.ValidateAmount(request.Amount);
        return Results.Ok(await service.PlaceBetAsync(context.UserId, context.DisplayName, NativeDiceRestSupport.Scope(context), request.Amount,
            NativeDiceRestSupport.SourceId(context, options, "bet"), ct));
    }

    private static async Task<IResult> PlayAsync(NativeDicePlayRequest request, RestRequestContext context, IBasketballService service,
        IOptions<RestFrameworkOptions> options, CancellationToken ct)
    {
        NativeDiceRestSupport.ValidateAmount(request.Amount);
        var scope = NativeDiceRestSupport.Scope(context);
        var source = NativeDiceRestSupport.SourceId(context, options, "play");
        var bet = await service.PlaceBetAsync(context.UserId, context.DisplayName, scope, request.Amount, source, ct);
        if (bet.Error != BasketballBetError.None) return Results.Ok(new BasketballPlayResponse(bet, null, 0));
        var face = NativeDiceRestSupport.RandomFace(5);
        var roll = await service.ThrowAsync(context.UserId, context.DisplayName, scope, face, source, ct);
        return Results.Ok(new BasketballPlayResponse(bet, roll, face));
    }

    private static async Task<IResult> RollAsync(NativeDiceRollRequest request, RestRequestContext context, IBasketballService service,
        IOptions<RestFrameworkOptions> options, CancellationToken ct)
    {
        NativeDiceRestSupport.ValidateFace(request.Face, 5);
        return Results.Ok(await service.ThrowAsync(context.UserId, context.DisplayName, NativeDiceRestSupport.Scope(context), request.Face,
            NativeDiceRestSupport.SourceId(context, options, "roll"), ct));
    }
}
