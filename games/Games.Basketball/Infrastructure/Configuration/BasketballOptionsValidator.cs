using BotFramework.Host.Configuration.Validation;
using FluentValidation;
using Games.Basketball.Domain.Configuration;

namespace Games.Basketball.Infrastructure.Configuration;

public sealed class BasketballOptionsValidator : FluentConfigurationValidator<BasketballOptions>
{
    public BasketballOptionsValidator()
    {
        RuleFor(x => x.DefaultBet).GreaterThan(0);
        RuleFor(x => x.MaxBet).GreaterThanOrEqualTo(x => x.DefaultBet);
        RuleFor(x => x.RedeemDropChance).InclusiveBetween(0, 1);
    }
}
