using System.Security.Cryptography;
using System.Text;
using BotFramework.Sdk.Execution;
using Games.Pick.Domain.Events;

namespace Games.Pick.Application.Execution;

public sealed record QuickLotteryJoinCommand(long UserId, string DisplayName, long ChatId, string CommandId);
