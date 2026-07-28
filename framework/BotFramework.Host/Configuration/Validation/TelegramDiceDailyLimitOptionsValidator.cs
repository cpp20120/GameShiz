using BotFramework.Host.Economics.Options;
using FluentValidation;

namespace BotFramework.Host.Configuration.Validation;

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
