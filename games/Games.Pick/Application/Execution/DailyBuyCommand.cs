using BotFramework.Sdk.Execution;
using Games.Pick.Domain.Events;

namespace Games.Pick.Application.Execution;

public sealed record DailyBuyCommand(long UserId,string DisplayName,long ChatId,int Count,string CommandId,DateOnly DayLocal,DateTime DeadlineUtc,int TicketPrice,int PerCommandCap,int UserCap);
