using BotFramework.Rest;
using Games.Redeem.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Games.Redeem.Rest;

public sealed record RedeemCompleteRequest(Guid CodeGuid);
