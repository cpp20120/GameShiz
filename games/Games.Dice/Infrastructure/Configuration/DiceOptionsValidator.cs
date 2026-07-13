using BotFramework.Host.Configuration.Validation;
using FluentValidation;
using Games.Dice.Domain.Configuration;

namespace Games.Dice.Infrastructure.Configuration;

public sealed class DiceOptionsValidator : FluentConfigurationValidator<DiceOptions>
{
    public DiceOptionsValidator()
    {
        RuleFor(x => x.Cost).GreaterThan(0);
        RuleFor(x => x.RedeemDropChance).InclusiveBetween(0, 1);
    }
}
