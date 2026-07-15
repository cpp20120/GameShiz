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

public sealed record NativeDicePlayRequest(int Amount);
public sealed record NativeDiceRollRequest(int Face);
public sealed record DiceCubePlayResponse(CubeBetResult Bet, CubeRollResult? Roll, int Face);
public sealed record FootballPlayResponse(FootballBetResult Bet, FootballThrowResult? Roll, int Face);
public sealed record BasketballPlayResponse(BasketballBetResult Bet, BasketballThrowResult? Roll, int Face);
public sealed record BowlingPlayResponse(BowlingBetResult Bet, BowlingRollResult? Roll, int Face);
public sealed record DartsPlayResponse(DartsThrowResult Result, int Face);

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
        long.TryParse(context.ScopeId, out var value)
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
            request.Amount, NativeDiceRestSupport.SourceId(context, options, "bet"), ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> PlayAsync(
        NativeDicePlayRequest request, RestRequestContext context, IDiceCubeService service,
        IOptions<RestFrameworkOptions> options, CancellationToken ct)
    {
        NativeDiceRestSupport.ValidateAmount(request.Amount);
        var scope = NativeDiceRestSupport.Scope(context);
        var source = NativeDiceRestSupport.SourceId(context, options, "play");
        var bet = await service.PlaceBetAsync(context.UserId, context.DisplayName, scope, request.Amount, source, ct).ConfigureAwait(false);
        if (bet.Error != CubeBetError.None)
            return Results.Ok(new DiceCubePlayResponse(bet, null, 0));
        var face = NativeDiceRestSupport.RandomFace(6);
        var roll = await service.RollAsync(context.UserId, context.DisplayName, scope, face, source, ct).ConfigureAwait(false);
        return Results.Ok(new DiceCubePlayResponse(bet, roll, face));
    }

    private static async Task<IResult> RollAsync(
        NativeDiceRollRequest request, RestRequestContext context, IDiceCubeService service,
        IOptions<RestFrameworkOptions> options, CancellationToken ct)
    {
        NativeDiceRestSupport.ValidateFace(request.Face, 6);
        var result = await service.RollAsync(context.UserId, context.DisplayName, NativeDiceRestSupport.Scope(context), request.Face,
            NativeDiceRestSupport.SourceId(context, options, "roll"), ct).ConfigureAwait(false);
        return Results.Ok(result);
    }
}

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
            NativeDiceRestSupport.SourceId(context, options, "play"), face, request.Amount, ct).ConfigureAwait(false);
        return Results.Ok(new DartsPlayResponse(result, face));
    }

    private static async Task<IResult> BetAsync(
        NativeDicePlayRequest request, RestRequestContext context, IDartsService service,
        IOptions<RestFrameworkOptions> options, CancellationToken ct)
    {
        NativeDiceRestSupport.ValidateAmount(request.Amount);
        var result = await service.PlaceBetAsync(context.UserId, context.DisplayName, NativeDiceRestSupport.Scope(context), request.Amount,
            NativeDiceRestSupport.SourceId(context, options, "bet"), ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> ThrowAsync(
        NativeDiceRollRequest request, long roundId, RestRequestContext context, IDartsService service,
        IOptions<RestFrameworkOptions> options, CancellationToken ct)
    {
        NativeDiceRestSupport.ValidateFace(request.Face, 6);
        var result = await service.ThrowAsync(roundId, context.UserId, context.DisplayName, NativeDiceRestSupport.Scope(context),
            NativeDiceRestSupport.SourceId(context, options, "throw"), request.Face, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }
}

public sealed class FootballRestModule : IRestRouteModule
{
    public string ModuleId => "football";

    public void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapRestGroup(ModuleId);
        group.MapPost("/bet", BetAsync).WithName("FootballBet").WithSummary("Place a Football bet");
        group.MapPost("/play", PlayAsync).WithName("FootballPlay").WithSummary("Place and resolve a Football bet");
        group.MapPost("/roll", RollAsync).WithName("FootballRoll").WithSummary("Resolve a pending Football bet");
    }

    private static async Task<IResult> BetAsync(NativeDicePlayRequest request, RestRequestContext context, IFootballService service,
        IOptions<RestFrameworkOptions> options, CancellationToken ct)
    {
        NativeDiceRestSupport.ValidateAmount(request.Amount);
        return Results.Ok(await service.PlaceBetAsync(context.UserId, context.DisplayName, NativeDiceRestSupport.Scope(context), request.Amount,
            NativeDiceRestSupport.SourceId(context, options, "bet"), ct).ConfigureAwait(false));
    }

    private static async Task<IResult> PlayAsync(NativeDicePlayRequest request, RestRequestContext context, IFootballService service,
        IOptions<RestFrameworkOptions> options, CancellationToken ct)
    {
        NativeDiceRestSupport.ValidateAmount(request.Amount);
        var scope = NativeDiceRestSupport.Scope(context);
        var source = NativeDiceRestSupport.SourceId(context, options, "play");
        var bet = await service.PlaceBetAsync(context.UserId, context.DisplayName, scope, request.Amount, source, ct).ConfigureAwait(false);
        if (bet.Error != FootballBetError.None) return Results.Ok(new FootballPlayResponse(bet, null, 0));
        var face = NativeDiceRestSupport.RandomFace(5);
        var roll = await service.ThrowAsync(context.UserId, context.DisplayName, scope, face, source, ct).ConfigureAwait(false);
        return Results.Ok(new FootballPlayResponse(bet, roll, face));
    }

    private static async Task<IResult> RollAsync(NativeDiceRollRequest request, RestRequestContext context, IFootballService service,
        IOptions<RestFrameworkOptions> options, CancellationToken ct)
    {
        NativeDiceRestSupport.ValidateFace(request.Face, 5);
        return Results.Ok(await service.ThrowAsync(context.UserId, context.DisplayName, NativeDiceRestSupport.Scope(context), request.Face,
            NativeDiceRestSupport.SourceId(context, options, "roll"), ct).ConfigureAwait(false));
    }
}

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
            NativeDiceRestSupport.SourceId(context, options, "bet"), ct).ConfigureAwait(false));
    }

    private static async Task<IResult> PlayAsync(NativeDicePlayRequest request, RestRequestContext context, IBasketballService service,
        IOptions<RestFrameworkOptions> options, CancellationToken ct)
    {
        NativeDiceRestSupport.ValidateAmount(request.Amount);
        var scope = NativeDiceRestSupport.Scope(context);
        var source = NativeDiceRestSupport.SourceId(context, options, "play");
        var bet = await service.PlaceBetAsync(context.UserId, context.DisplayName, scope, request.Amount, source, ct).ConfigureAwait(false);
        if (bet.Error != BasketballBetError.None) return Results.Ok(new BasketballPlayResponse(bet, null, 0));
        var face = NativeDiceRestSupport.RandomFace(5);
        var roll = await service.ThrowAsync(context.UserId, context.DisplayName, scope, face, source, ct).ConfigureAwait(false);
        return Results.Ok(new BasketballPlayResponse(bet, roll, face));
    }

    private static async Task<IResult> RollAsync(NativeDiceRollRequest request, RestRequestContext context, IBasketballService service,
        IOptions<RestFrameworkOptions> options, CancellationToken ct)
    {
        NativeDiceRestSupport.ValidateFace(request.Face, 5);
        return Results.Ok(await service.ThrowAsync(context.UserId, context.DisplayName, NativeDiceRestSupport.Scope(context), request.Face,
            NativeDiceRestSupport.SourceId(context, options, "roll"), ct).ConfigureAwait(false));
    }
}

public sealed class BowlingRestModule : IRestRouteModule
{
    public string ModuleId => "bowling";

    public void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapRestGroup(ModuleId);
        group.MapPost("/bet", BetAsync).WithName("BowlingBet").WithSummary("Place a Bowling bet");
        group.MapPost("/play", PlayAsync).WithName("BowlingPlay").WithSummary("Place and resolve a Bowling bet");
        group.MapPost("/roll", RollAsync).WithName("BowlingRoll").WithSummary("Resolve a pending Bowling bet");
    }

    private static async Task<IResult> BetAsync(NativeDicePlayRequest request, RestRequestContext context, IBowlingService service,
        IOptions<RestFrameworkOptions> options, CancellationToken ct)
    {
        NativeDiceRestSupport.ValidateAmount(request.Amount);
        return Results.Ok(await service.PlaceBetAsync(context.UserId, context.DisplayName, NativeDiceRestSupport.Scope(context), request.Amount,
            NativeDiceRestSupport.SourceId(context, options, "bet"), ct).ConfigureAwait(false));
    }

    private static async Task<IResult> PlayAsync(NativeDicePlayRequest request, RestRequestContext context, IBowlingService service,
        IOptions<RestFrameworkOptions> options, CancellationToken ct)
    {
        NativeDiceRestSupport.ValidateAmount(request.Amount);
        var scope = NativeDiceRestSupport.Scope(context);
        var source = NativeDiceRestSupport.SourceId(context, options, "play");
        var bet = await service.PlaceBetAsync(context.UserId, context.DisplayName, scope, request.Amount, source, ct).ConfigureAwait(false);
        if (bet.Error != BowlingBetError.None) return Results.Ok(new BowlingPlayResponse(bet, null, 0));
        var face = NativeDiceRestSupport.RandomFace(6);
        var roll = await service.RollAsync(context.UserId, context.DisplayName, scope, face, source, ct).ConfigureAwait(false);
        return Results.Ok(new BowlingPlayResponse(bet, roll, face));
    }

    private static async Task<IResult> RollAsync(NativeDiceRollRequest request, RestRequestContext context, IBowlingService service,
        IOptions<RestFrameworkOptions> options, CancellationToken ct)
    {
        NativeDiceRestSupport.ValidateFace(request.Face, 6);
        return Results.Ok(await service.RollAsync(context.UserId, context.DisplayName, NativeDiceRestSupport.Scope(context), request.Face,
            NativeDiceRestSupport.SourceId(context, options, "roll"), ct).ConfigureAwait(false));
    }
}

public static class NativeDiceRestServiceCollectionExtensions
{
    public static IServiceCollection AddNativeDiceRest(this IServiceCollection services) => services
        .AddRestRouteModule<DiceCubeRestModule>()
        .AddRestRouteModule<DartsRestModule>()
        .AddRestRouteModule<FootballRestModule>()
        .AddRestRouteModule<BasketballRestModule>()
        .AddRestRouteModule<BowlingRestModule>();
}
