using BotFramework.Host.Configuration.Validation;
using FluentValidation;
using Games.Pick.Domain.Configuration;

namespace Games.Pick.Infrastructure.Configuration;

public sealed class PickOptionsValidator : FluentConfigurationValidator<PickOptions>
{
    public PickOptionsValidator()
    {
        RuleFor(x => x.DefaultBet).GreaterThan(0);
        RuleFor(x => x.MaxBet).Must((x, value) => value == 0 || value >= x.DefaultBet);
        RuleFor(x => x.MinVariants).GreaterThanOrEqualTo(2);
        RuleFor(x => x.MaxVariants).GreaterThanOrEqualTo(x => x.MinVariants);
        RuleFor(x => x.HouseEdge).GreaterThanOrEqualTo(0).LessThan(1);
        RuleFor(x => x.StreakBonusPerWin).GreaterThanOrEqualTo(0);
        RuleFor(x => x.StreakCap).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ChainMaxDepth).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ChainTtlSeconds).GreaterThan(0);
        RuleFor(x => x.Lottery.DurationSeconds).GreaterThan(0);
        RuleFor(x => x.Lottery.MinEntrantsToSettle).GreaterThanOrEqualTo(2);
        RuleFor(x => x.Lottery.HouseFeePercent).GreaterThanOrEqualTo(0).LessThan(1);
        RuleFor(x => x.Lottery.MinStake).GreaterThan(0);
        RuleFor(x => x.Lottery.MaxStake).Must((x, value) => value == 0 || value >= x.Lottery.MinStake);
        RuleFor(x => x.Daily.TicketPrice).GreaterThan(0);
        RuleFor(x => x.Daily.MaxTicketsPerUserPerDay).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Daily.MaxTicketsPerBuyCommand).GreaterThan(0);
        RuleFor(x => x.Daily.HouseFeePercent).GreaterThanOrEqualTo(0).LessThan(1);
        RuleFor(x => x.Daily.HistoryLimit).GreaterThan(0);
        RuleFor(x => x.Daily.DrawHourLocal).InclusiveBetween(0, 23);
        RuleFor(x => x.Daily.TimezoneOffsetHoursOverride).InclusiveBetween(-14, 14);
    }
}
