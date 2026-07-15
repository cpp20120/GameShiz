using BotFramework.Contracts.Messaging;
using BotFramework.Rest;
using Games.Dice.Contracts.Play;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Games.Dice.Rest;

public sealed record DiceRollRequest(int SlotValue);

public sealed class DiceRestModule : IRestRouteModule
{
    public string ModuleId => "dice";

    public void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapRestGroup(ModuleId);
        group.MapPost("/roll", RollAsync)
            .WithName("DiceRoll")
            .WithSummary("Roll the slots dice")
            .Produces<DicePlayResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<IResult> RollAsync(
        DiceRollRequest request,
        RestRequestContext context,
        IDiceClient client,
        IOptions<RestFrameworkOptions> options,
        CancellationToken cancellationToken)
    {
        if (request.SlotValue is < 1 or > 64)
            throw new RestBadRequestException("SlotValue must be between 1 and 64.");

        var idempotencyKey = context.IdempotencyKey;
        if (options.Value.RequireIdempotencyKeyForCommands)
            idempotencyKey = context.RequireIdempotencyKey();
        idempotencyKey ??= context.RequestId;

        if (!long.TryParse(context.ScopeId, out var scopeId))
            throw new RestBadRequestException("scopeId must be a numeric balance scope.");

        var response = await client.PlayAsync(
            new DicePlayRequest(
                context.UserId,
                context.DisplayName,
                request.SlotValue,
                scopeId,
                RestIdempotency.ToStableSourceId(idempotencyKey).ToString(System.Globalization.CultureInfo.InvariantCulture),
                false),
            new RequestMetadata(
                context.RequestId,
                context.CorrelationId,
                "rest",
                context.Subject,
                context.ScopeId,
                "en",
                context.Baggage),
            cancellationToken).ConfigureAwait(false);

        return Results.Ok(response);
    }
}

public static class DiceRestServiceCollectionExtensions
{
    public static IServiceCollection AddDiceRest(this IServiceCollection services) =>
        services.AddRestRouteModule<DiceRestModule>();
}
