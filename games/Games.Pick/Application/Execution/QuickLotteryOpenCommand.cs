using System.Security.Cryptography;
using System.Text;
using BotFramework.Sdk.Execution;
using Games.Pick.Domain.Events;

namespace Games.Pick.Application.Execution;

public sealed record QuickLotteryOpenCommand(long UserId, string DisplayName, long ChatId, int Stake, string CommandId, int MinStake, int MaxStake, int DurationSeconds);
