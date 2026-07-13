using BotFramework.Sdk.Execution;

namespace Games.Pick.Application.Execution;

public sealed record PickChainOfferEffect(PickChainState Chain) : IGameEffect;
