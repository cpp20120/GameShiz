using BotFramework.Sdk.Execution;
using Games.Pick.Domain.Events;

namespace Games.Pick.Application.Execution;

public sealed record DailySettleCommand(PickDailyLotteryRow Row,IReadOnlyList<DailyTicketOwner> ExpectedTickets,string CommandId,double HouseFee);
