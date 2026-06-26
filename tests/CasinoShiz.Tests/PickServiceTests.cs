using Games.Pick.Application.Services;
using Games.Pick.Domain.Configuration;
using Games.Pick.Domain.Results;
using Games.Pick.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class PickServiceTests
{
    private static PickService MakeService(FakeEconomicsService? economics = null, PickOptions? options = null) =>
        new(
            economics ?? new FakeEconomicsService(),
            new NullAnalyticsService(),
            new PickStreakStore(),
            new PickChainStore(),
            Options.Create(options ?? new PickOptions { MinVariants = 2, MaxVariants = 4, MaxBet = 100 }),
            NullLogger<PickService>.Instance);

    [Fact]
    public async Task PickAsync_NotEnoughVariants_ReturnsValidationErrorWithoutDebit()
    {
        var economics = new FakeEconomicsService();
        var result = await MakeService(economics).PickAsync(1, "u", 100, 10, ["only"], [0], default);

        Assert.Equal(PickError.NotEnoughVariants, result.Error);
        Assert.Empty(economics.Debits);
    }

    [Fact]
    public async Task PickAsync_TooManyVariants_ReturnsValidationErrorWithoutDebit()
    {
        var economics = new FakeEconomicsService();
        var result = await MakeService(economics).PickAsync(1, "u", 100, 10, ["a", "b", "c", "d", "e"], [0], default);

        Assert.Equal(PickError.TooManyVariants, result.Error);
        Assert.Empty(economics.Debits);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task PickAsync_InvalidAmount_ReturnsValidationErrorWithoutDebit(int amount)
    {
        var economics = new FakeEconomicsService();
        var result = await MakeService(economics).PickAsync(1, "u", 100, amount, ["a", "b"], [0], default);

        Assert.Equal(PickError.InvalidAmount, result.Error);
        Assert.Empty(economics.Debits);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public async Task PickAsync_ChoiceOutOfRange_ReturnsValidationErrorWithoutDebit(int backedIndex)
    {
        var economics = new FakeEconomicsService();
        var result = await MakeService(economics).PickAsync(1, "u", 100, 10, ["a", "b"], [backedIndex], default);

        Assert.Equal(PickError.InvalidChoice, result.Error);
        Assert.Empty(economics.Debits);
    }

    [Fact]
    public async Task PickAsync_BackAllVariants_ReturnsInvalidChoiceWithoutDebit()
    {
        var economics = new FakeEconomicsService();
        var result = await MakeService(economics).PickAsync(1, "u", 100, 10, ["a", "b"], [0, 1], default);

        Assert.Equal(PickError.InvalidChoice, result.Error);
        Assert.Empty(economics.Debits);
    }

    [Fact]
    public async Task PickAsync_NotEnoughCoins_ReturnsBalanceWithoutDebit()
    {
        var economics = new FakeEconomicsService { StartingBalance = 9 };
        var result = await MakeService(economics).PickAsync(1, "u", 100, 10, ["a", "b"], [0], default);

        Assert.Equal(PickError.NotEnoughCoins, result.Error);
        Assert.Equal(9, result.Balance);
        Assert.Empty(economics.Debits);
    }
}
