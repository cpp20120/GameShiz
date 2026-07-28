using BotFramework.Sdk.Execution;
using Games.Pick.Domain.Events;

namespace Games.Pick.Application.Execution;

public sealed record DailyTicketOwner(long UserId,string DisplayName);
