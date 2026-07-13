using BotFramework.Host.Configuration.Validation;
using FluentValidation;
using Games.Bowling.Domain.Configuration;

namespace Games.Bowling.Infrastructure.Configuration;

public sealed class BowlingOptionsValidator : FluentConfigurationValidator<BowlingOptions>
{
    public BowlingOptionsValidator()
    {
        RuleFor(x => x.DefaultBet).GreaterThan(0);
        RuleFor(x => x.MaxBet).GreaterThanOrEqualTo(x => x.DefaultBet);
        RuleFor(x => x.RedeemDropChance).InclusiveBetween(0, 1);
    }
}
