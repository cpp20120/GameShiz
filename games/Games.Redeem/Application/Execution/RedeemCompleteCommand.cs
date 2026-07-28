using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using Microsoft.Extensions.Options;

namespace Games.Redeem.Application.Execution;

public sealed record RedeemCompleteCommand(
    Guid Code,
    long UserId,
    long BalanceScopeId,
    string ExpectedGameId,
    string CommandId);
