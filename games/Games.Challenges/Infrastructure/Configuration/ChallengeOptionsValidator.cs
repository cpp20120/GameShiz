using BotFramework.Host.Configuration.Validation;
using FluentValidation;
using Games.Challenges.Domain.Configuration;

namespace Games.Challenges.Infrastructure.Configuration;

public sealed class ChallengeOptionsValidator : FluentConfigurationValidator<ChallengeOptions>
{
    public ChallengeOptionsValidator()
    {
        RuleFor(x => x.MinBet).GreaterThan(0);
        RuleFor(x => x.MaxBet).GreaterThanOrEqualTo(x => x.MinBet);
        RuleFor(x => x.HouseFeeBasisPoints).InclusiveBetween(0, 9_999);
        RuleFor(x => x.PendingTtlMinutes).InclusiveBetween(1, 60);
    }
}
