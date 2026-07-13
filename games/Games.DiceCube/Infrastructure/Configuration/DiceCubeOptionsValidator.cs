using BotFramework.Host.Configuration.Validation;
using FluentValidation;
using Games.DiceCube.Domain.Configuration;

namespace Games.DiceCube.Infrastructure.Configuration;

public sealed class DiceCubeOptionsValidator : FluentConfigurationValidator<DiceCubeOptions>
{
    public DiceCubeOptionsValidator()
    {
        RuleFor(x => x.DefaultBet).GreaterThan(0);
        RuleFor(x => x.MaxBet).GreaterThanOrEqualTo(x => x.DefaultBet);
        RuleFor(x => x.RedeemDropChance).InclusiveBetween(0, 1);
    }
}
