using BotFramework.Host.Configuration.Validation;
using FluentValidation;
using Games.Meta.Domain.Seasons;

namespace Games.Meta.Infrastructure.Configuration;

public sealed class MetaOptionsValidator : FluentConfigurationValidator<MetaOptions>
{
    public MetaOptionsValidator()
    {
        RuleFor(x => x.HighRollerTotalStaked).GreaterThan(0);
        RuleFor(x => x.BigPayoutMinimum).GreaterThan(0);
        RuleFor(x => x.StreakTimezoneOffsetHours).InclusiveBetween(-14, 14);
    }
}
