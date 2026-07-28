using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using Microsoft.Extensions.Options;

namespace Games.Redeem.Application.Execution;

public sealed record RedeemExecutionState(RedeemCode? Code);
