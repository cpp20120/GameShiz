using BotFramework.Sdk.Economics;
using BotFramework.Sdk.Execution;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class ResponsibleGamingPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PlayerProtection_SelfExclusionHasPrecedence()
    {
        var decision = PlayerProtectionPolicy.Evaluate(
            new PlayerProtectionState(
                DailyStakeLimit: 10,
                CooldownUntil: Now.AddHours(1),
                SelfExcludedUntil: Now.AddDays(1),
                UsedToday: 10),
            stake: 1,
            Now);

        Assert.False(decision.Allowed);
        Assert.Equal("self_excluded", decision.ReasonCode);
        Assert.Equal(Now.AddDays(1), decision.BlockedUntil);
    }

    [Fact]
    public void PlayerProtection_DailyLimitAllowsExactBoundary()
    {
        var decision = PlayerProtectionPolicy.Evaluate(
            new PlayerProtectionState(100, null, null, UsedToday: 70),
            stake: 30,
            Now);

        Assert.True(decision.Allowed);
        Assert.Null(decision.ReasonCode);
    }

    [Fact]
    public void PlayerProtection_DailyLimitReturnsUsageDetails()
    {
        var decision = PlayerProtectionPolicy.Evaluate(
            new PlayerProtectionState(100, null, null, UsedToday: 70),
            stake: 31,
            Now);

        Assert.False(decision.Allowed);
        Assert.Equal("daily_limit", decision.ReasonCode);
        Assert.Equal(100, decision.DailyStakeLimit);
        Assert.Equal(70, decision.UsedToday);
    }

    [Fact]
    public void QuotaPolicy_RejectsWholeBatchWhenFinalUsageExceedsLimit()
    {
        var decision = QuotaPolicy.Apply(
            new QuotaSnapshot(9, 10),
            "dice",
            [QuotaEffect.Consume("dice"), QuotaEffect.Consume("dice")]);

        Assert.False(decision.Applied);
        Assert.True(decision.Rejected);
        Assert.Equal(9, decision.NewUsed);
        Assert.Equal(10, decision.Limit);
    }

    [Fact]
    public void QuotaPolicy_RestoreClampsAtZeroButGrantCanGoNegative()
    {
        var restored = QuotaPolicy.Apply(
            new QuotaSnapshot(2, 10),
            "dice",
            [QuotaEffect.Restore("dice", 10)]);
        var granted = QuotaPolicy.Apply(
            new QuotaSnapshot(2, 10),
            "dice",
            [QuotaEffect.Grant("dice", 10)]);

        Assert.Equal(0, restored.NewUsed);
        Assert.Equal(-8, granted.NewUsed);
    }

    [Property(MaxTest = 200)]
    public Property PlayerProtection_CommandSequence_PreservesDailyLimitAndBlockPrecedence(
        NonEmptyArray<int> commands)
    {
        const long dailyLimit = 500;
        var usedToday = 0L;
        string? failure = null;

        foreach (var rawCommand in commands.Get)
        {
            var magnitude = Math.Abs((long)rawCommand);
            var stake = 1 + magnitude % 40;
            var commandNow = Now.AddHours(magnitude % 8);
            var selfExcludedUntil = magnitude % 11 == 0 ? commandNow.AddHours(1) : (DateTimeOffset?)null;
            var cooldownUntil = magnitude % 7 == 0 ? commandNow.AddMinutes(30) : (DateTimeOffset?)null;
            var before = usedToday;
            var decision = PlayerProtectionPolicy.Evaluate(
                new PlayerProtectionState(
                    (int)dailyLimit,
                    cooldownUntil,
                    selfExcludedUntil,
                    usedToday),
                stake,
                commandNow);

            var expectedReason = selfExcludedUntil is not null
                ? "self_excluded"
                : cooldownUntil is not null
                    ? "cooldown"
                    : before + stake > dailyLimit
                        ? "daily_limit"
                        : null;

            if (decision.Allowed != (expectedReason is null)
                || decision.ReasonCode != expectedReason)
            {
                failure = $"unexpected protection decision: expected={expectedReason}, actual={decision.ReasonCode}";
                break;
            }

            if (decision.Allowed)
                usedToday = checked(usedToday + stake);

            if (usedToday != before && (expectedReason is not null || usedToday > dailyLimit))
            {
                failure = "blocked wager changed daily usage";
                break;
            }
        }

        return (failure is null)
            .ToProperty()
            .Label(failure ?? $"commands={commands.Get.Length}, used={usedToday}");
    }

    [Property(MaxTest = 200)]
    public Property Quota_CommandSequence_PreservesAtomicUsageAndLimit(NonEmptyArray<int> commands)
    {
        const string quotaId = "dice";
        const long limit = 100;
        var state = new QuotaSnapshot(0, limit);
        string? failure = null;

        foreach (var rawCommand in commands.Get)
        {
            var magnitude = Math.Abs((long)rawCommand);
            var amount = 1 + magnitude % 25;
            var kind = (QuotaEffectKind)(magnitude % 3);
            var before = state;
            var effect = new QuotaEffect(quotaId, kind, amount);
            var expectedUsage = kind switch
            {
                QuotaEffectKind.Consume => checked(before.Used + amount),
                QuotaEffectKind.Restore => Math.Max(0, before.Used - amount),
                QuotaEffectKind.Grant => checked(before.Used - amount),
                _ => throw new ArgumentOutOfRangeException(),
            };
            var decision = QuotaPolicy.Apply(before, quotaId, [effect]);

            if (expectedUsage > limit)
            {
                if (decision.Applied || !decision.Rejected || decision.NewUsed != before.Used)
                {
                    failure = "quota rejection was not atomic";
                    break;
                }
            }
            else
            {
                if (!decision.Applied || decision.Rejected || decision.NewUsed != expectedUsage)
                {
                    failure = "quota accepted an unexpected usage transition";
                    break;
                }

                state = new QuotaSnapshot(decision.NewUsed, decision.Limit);
            }

            if (state.Used > state.Limit)
            {
                failure = "quota state exceeded its limit";
                break;
            }
        }

        return (failure is null)
            .ToProperty()
            .Label(failure ?? $"commands={commands.Get.Length}, used={state.Used}");
    }
}
