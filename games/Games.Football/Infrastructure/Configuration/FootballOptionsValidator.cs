using BotFramework.Host.Configuration.Validation;
using FluentValidation;
using Games.Football.Domain.Configuration;

namespace Games.Football.Infrastructure.Configuration;

public sealed class FootballOptionsValidator : FluentConfigurationValidator<FootballOptions>
{
    public FootballOptionsValidator()
    {
        RuleFor(x => x.DefaultBet).GreaterThan(0);
        RuleFor(x => x.MaxBet).GreaterThanOrEqualTo(x => x.DefaultBet);
        RuleFor(x => x.RedeemDropChance).InclusiveBetween(0, 1);
    }
}
