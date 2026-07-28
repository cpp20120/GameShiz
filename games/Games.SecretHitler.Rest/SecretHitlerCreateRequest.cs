using BotFramework.Rest;
using Games.SecretHitler.Application.Services;
using Games.SecretHitler.Domain.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Games.SecretHitler.Rest;

public sealed record SecretHitlerCreateRequest(long? PlayerChatId);
