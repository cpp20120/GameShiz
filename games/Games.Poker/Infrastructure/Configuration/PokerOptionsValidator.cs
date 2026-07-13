using BotFramework.Host.Configuration.Validation;
using FluentValidation;
using Games.Poker.Domain.Configuration;

namespace Games.Poker.Infrastructure.Configuration;

public sealed class PokerOptionsValidator : FluentConfigurationValidator<PokerOptions>
{
    public PokerOptionsValidator()
    {
        RuleFor(x => x.BuyIn).GreaterThan(0);
        RuleFor(x => x.SmallBlind).GreaterThan(0);
        RuleFor(x => x.BigBlind).GreaterThanOrEqualTo(x => x.SmallBlind);
        RuleFor(x => x.MaxPlayers).InclusiveBetween(2, 10);
        RuleFor(x => x.TurnTimeoutMs).GreaterThanOrEqualTo(5_000);
    }
}
