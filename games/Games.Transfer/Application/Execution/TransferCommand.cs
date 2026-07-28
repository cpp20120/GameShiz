using BotFramework.Host.Execution;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Sdk.Events.Meta;
using BotFramework.Sdk.Execution;

namespace Games.Transfer.Application.Execution;

public sealed record TransferCommand(
    long FromUserId,
    long ToUserId,
    long ChatId,
    string SenderDisplayName,
    string RecipientDisplayName,
    int NetToRecipient,
    int FeeCoins,
    int TotalDebited,
    string CommandId);
