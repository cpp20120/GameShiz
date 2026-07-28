using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using Microsoft.Extensions.Options;

namespace Games.Redeem.Application.Execution;

public sealed record RedeemIssueCommand(
    Guid Code,
    long IssuedBy,
    string FreeSpinGameId,
    string CommandId);
