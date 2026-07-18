using BotFramework.Host.Configuration.Validation;
using FluentValidation;
using Games.Horse.Domain.Configuration;

namespace Games.Horse.Infrastructure.Configuration;

public sealed class HorseOptionsValidator : FluentConfigurationValidator<HorseOptions>
{
    public HorseOptionsValidator()
    {
        RuleFor(x => x.HorseCount).InclusiveBetween(2, 16);
        RuleFor(x => x.MinBetsToRun).GreaterThan(0);
        RuleFor(x => x.AnnounceDelayMs).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AnnounceDelay1V1Ms).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RenderVariants).InclusiveBetween(1, 20);
        RuleFor(x => x.TimezoneOffsetHours).InclusiveBetween(-14, 14);
        RuleFor(x => x.AutoRunEveryDays).InclusiveBetween(1, 31);
        RuleFor(x => x.AutoRunLocalHour).InclusiveBetween(0, 23);
        RuleFor(x => x.AutoRunLocalMinute).InclusiveBetween(0, 59);
    }
}
