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

public static class ChallengesRestServiceCollectionExtensions
{
    public static IServiceCollection AddChallengesRest(this IServiceCollection services) => services.AddRestRouteModule<ChallengesRestModule>();
}
