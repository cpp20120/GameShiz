using BotFramework.Host.Execution;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Sdk.Events.Meta;
using BotFramework.Sdk.Execution;

namespace Games.Transfer.Application.Execution;

public sealed record TransferState(int RecipientBalance);
