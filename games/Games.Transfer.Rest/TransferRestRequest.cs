using BotFramework.Rest;
using Games.Transfer.Application.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Games.Transfer.Rest;

public sealed record TransferRestRequest(long ToUserId, string RecipientDisplayName, int Amount);
