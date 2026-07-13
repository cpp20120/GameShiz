using BotFramework.Host.Configuration.Validation;
using FluentValidation;
using Games.Darts.Domain.Configuration;

namespace Games.Darts.Infrastructure.Configuration;

public sealed class DartsOptionsValidator : FluentConfigurationValidator<DartsOptions>
{
    public DartsOptionsValidator()
    {
        RuleFor(x => x.DefaultBet).GreaterThan(0);
        RuleFor(x => x.MaxBet).GreaterThanOrEqualTo(x => x.DefaultBet);
        RuleFor(x => x.RedeemDropChance).InclusiveBetween(0, 1);
    }
}
