using BotFramework.Sdk.Execution;

namespace Games.Poker.Application.Execution;

public sealed record ActionResolution(
    ActionResult Result,
    IReadOnlyList<IGameEffect> Effects,
    IReadOnlyList<IDomainEvent> Events);
