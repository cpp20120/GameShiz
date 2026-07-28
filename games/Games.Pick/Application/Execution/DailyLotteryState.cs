using BotFramework.Sdk.Execution;
using Games.Pick.Domain.Events;

namespace Games.Pick.Application.Execution;

public sealed record DailyLotteryState(PickDailyLotteryRow Row,IReadOnlyList<DailyTicketOwner> Tickets);
