using BotFramework.Rest;
using Games.Transfer.Application.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Games.Transfer.Rest;

public static class TransferRestServiceCollectionExtensions
{
    public static IServiceCollection AddTransferRest(this IServiceCollection services) => services.AddRestRouteModule<TransferRestModule>();
}
