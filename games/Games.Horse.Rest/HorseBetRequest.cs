using BotFramework.Rest;
using Games.Horse.Application.Services;
using Games.Horse.Domain.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Games.Horse.Rest;

public sealed record HorseBetRequest(int HorseId, int Amount);
