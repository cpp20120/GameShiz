using BotFramework.Host.Configuration.RuntimeTuning;
using BotFramework.Host.Economics.Options;
using FluentValidation;

namespace BotFramework.Host.Configuration.Validation;

internal sealed class DailyBonusOptionsValidator : FluentConfigurationValidator<DailyBonusOptions>
{
    public DailyBonusOptionsValidator()
    {
        RuleFor(options => options.PercentOfBalance).InclusiveBetween(0, 100);
        RuleFor(options => options.MaxBonus).GreaterThanOrEqualTo(0);
        RuleFor(options => options.TimezoneOffsetHours).InclusiveBetween(-14, 14);
        RuleFor(options => options.MaxCatchUpDays).InclusiveBetween(0, 366);
    }
}

internal sealed class TelegramDiceDailyLimitOptionsValidator
    : FluentConfigurationValidator<TelegramDiceDailyLimitOptions>
{
    public TelegramDiceDailyLimitOptionsValidator()
    {
        RuleFor(options => options.MaxRollsPerUserPerDay).GreaterThanOrEqualTo(0);
        RuleFor(options => options.TimezoneOffsetHours).InclusiveBetween(-14, 14);
        RuleForEach(options => options.MaxRollsPerUserPerDayByGame)
            .Must(static pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value >= 0)
            .WithMessage("Game IDs cannot be empty and daily limits cannot be negative.");
    }
}
