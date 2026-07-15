using System.Security.Cryptography;
using BotFramework.Rest;
using Games.Challenges.Application.Services;
using Games.Challenges.Domain.Entities;
using Games.Challenges.Domain.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Games.Challenges.Rest;

public sealed record ChallengeCreateRequest(long TargetUserId, string TargetDisplayName, int Amount, ChallengeGame Game);

public sealed class ChallengesRestModule : IRestRouteModule
{
    public string ModuleId => "challenges";

    public void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapRestGroup(ModuleId);
        group.MapPost("", CreateAsync).WithName("ChallengeCreate");
        group.MapPost("/{challengeId:guid}/accept", AcceptAsync).WithName("ChallengeAccept");
        group.MapPost("/{challengeId:guid}/decline", DeclineAsync).WithName("ChallengeDecline");
    }

    private static async Task<IResult> CreateAsync(ChallengeCreateRequest request, IChallengeService service, RestRequestContext context, CancellationToken ct)
    {
        if (request.TargetUserId <= 0 || request.TargetUserId == context.UserId)
            throw new RestBadRequestException("TargetUserId must identify another player.");
        RestCommandSupport.RequireText(request.TargetDisplayName, nameof(request.TargetDisplayName));
        RestCommandSupport.RequirePositive(request.Amount, nameof(request.Amount));
        return Results.Ok(await service.CreateAsync(context.UserId, context.DisplayName,
            new ChallengeUser(request.TargetUserId, request.TargetDisplayName), RestCommandSupport.ScopeId(context), request.Amount, request.Game, ct)
            .ConfigureAwait(false));
    }

    private static async Task<IResult> AcceptAsync(Guid challengeId, IChallengeService service, RestRequestContext context, CancellationToken ct)
    {
        var accepted = await service.BeginAcceptAsync(challengeId, context.UserId, ct).ConfigureAwait(false);
        if (accepted.Error != ChallengeAcceptError.None || accepted.Challenge is null)
            return Results.Ok(accepted);
        try
        {
            var maxFace = accepted.Challenge.Game switch
            {
                ChallengeGame.Dice or ChallengeGame.DiceCube or ChallengeGame.Darts or ChallengeGame.Bowling => 6,
                ChallengeGame.Basketball or ChallengeGame.Football => 5,
                ChallengeGame.Slots => 64,
                ChallengeGame.Horse => 2,
                ChallengeGame.Blackjack => 22,
                _ => 6,
            };
            var challengerRoll = RandomNumberGenerator.GetInt32(1, maxFace + 1);
            var targetRoll = RandomNumberGenerator.GetInt32(1, maxFace + 1);
            return Results.Ok(await service.CompleteAcceptedAsync(accepted.Challenge, challengerRoll, targetRoll, ct).ConfigureAwait(false));
        }
        catch
        {
            await service.FailAcceptedAsync(accepted.Challenge, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<IResult> DeclineAsync(Guid challengeId, IChallengeService service, RestRequestContext context, CancellationToken ct) =>
        Results.Ok(await service.DeclineAsync(challengeId, context.UserId, ct).ConfigureAwait(false));
}

public static class ChallengesRestServiceCollectionExtensions
{
    public static IServiceCollection AddChallengesRest(this IServiceCollection services) => services.AddRestRouteModule<ChallengesRestModule>();
}
