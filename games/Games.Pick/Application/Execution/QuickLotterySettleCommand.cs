using System.Security.Cryptography;
using System.Text;
using BotFramework.Sdk.Execution;
using Games.Pick.Domain.Events;

namespace Games.Pick.Application.Execution;

public sealed record QuickLotterySettleCommand(PickLotteryRow Row, IReadOnlyList<PickLotteryEntryRow> ExpectedEntries, bool ForceCancel, string CommandId, int MinEntrants, double HouseFee);
