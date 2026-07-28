using BotFramework.Rest;
using Games.Admin.Application.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Games.Admin.Rest;

public sealed record AdminPayRequest(long TargetUserId, long BalanceScopeId, int Amount);
