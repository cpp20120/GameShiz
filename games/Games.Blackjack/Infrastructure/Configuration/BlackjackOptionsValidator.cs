using BotFramework.Host.Configuration.Validation;
using FluentValidation;
using Games.Blackjack.Domain.Configuration;

namespace Games.Blackjack.Infrastructure.Configuration;

public sealed class BlackjackOptionsValidator : FluentConfigurationValidator<BlackjackOptions>
{
    public BlackjackOptionsValidator()
    {
        RuleFor(x => x.MinBet).GreaterThan(0);
        RuleFor(x => x.MaxBet).GreaterThanOrEqualTo(x => x.MinBet);
        RuleFor(x => x.HandTimeoutMs).GreaterThanOrEqualTo(5_000);
    }
}
