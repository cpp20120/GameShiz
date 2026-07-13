using BotFramework.Host.Configuration.Validation;
using FluentValidation;
using Games.SecretHitler.Domain.Configuration;

namespace Games.SecretHitler.Infrastructure.Configuration;

public sealed class SecretHitlerOptionsValidator : FluentConfigurationValidator<SecretHitlerOptions>
{
    public SecretHitlerOptionsValidator() => RuleFor(x => x.BuyIn).GreaterThan(0);
}
