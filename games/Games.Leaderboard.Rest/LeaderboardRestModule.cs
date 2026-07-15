using BotFramework.Rest;
using Games.Leaderboard.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Games.Leaderboard.Rest;

public sealed class LeaderboardRestModule : IRestRouteModule
{
    public string ModuleId => "leaderboard";

    public void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapRestGroup(ModuleId)
            .MapGet("", GetTopAsync)
            .WithName("LeaderboardGetTop")
            .WithSummary("Read the leaderboard for the current scope")
            .Produces<Games.Leaderboard.Domain.Models.Leaderboard>()
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> GetTopAsync(
        int? limit,
        RestRequestContext context,
        ILeaderboardClient client,
        CancellationToken cancellationToken)
    {
        if (!long.TryParse(context.ScopeId, out var scopeId))
            throw new RestBadRequestException("scopeId must be a numeric leaderboard scope.");
        var actualLimit = limit ?? 15;
        if (actualLimit is < 1 or > 100)
            throw new RestBadRequestException("limit must be between 1 and 100.");

        var result = await client.GetTopAsync(actualLimit, scopeId, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }
}

public static class LeaderboardRestServiceCollectionExtensions
{
    public static IServiceCollection AddLeaderboardRest(this IServiceCollection services) =>
        services.AddRestRouteModule<LeaderboardRestModule>();
}
