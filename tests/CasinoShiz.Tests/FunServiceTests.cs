using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Games.Fun.Application;
using Games.Fun.Domain;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class FunServiceTests
{
    [Fact]
    public void Roll_WithoutQuestion_ReturnsPercentageOnly()
    {
        var service = new FunService(new FixedRandomSource(42));

        var result = service.Roll(null);

        Assert.Equal(42, result.Percentage);
        Assert.Null(result.Question);
        Assert.Null(result.FavorableCases);
        Assert.Null(result.TotalCases);
    }

    [Fact]
    public void Roll_WithQuestion_ReducesFraction()
    {
        var service = new FunService(new FixedRandomSource(42));

        var result = service.Roll(" успеем ли выкатить сегодня ");

        Assert.Equal("успеем ли выкатить сегодня", result.Question);
        Assert.Equal(21, result.FavorableCases);
        Assert.Equal(50, result.TotalCases);
        Assert.Equal(RollBand.Uncertain, result.Band);
    }

    [Theory]
    [InlineData(0, RollBand.VeryUnlikely)]
    [InlineData(19, RollBand.VeryUnlikely)]
    [InlineData(20, RollBand.Unlikely)]
    [InlineData(39, RollBand.Unlikely)]
    [InlineData(40, RollBand.Uncertain)]
    [InlineData(59, RollBand.Uncertain)]
    [InlineData(60, RollBand.Likely)]
    [InlineData(79, RollBand.Likely)]
    [InlineData(80, RollBand.VeryLikely)]
    [InlineData(100, RollBand.VeryLikely)]
    public void RollRules_UsesExpectedBands(int percentage, RollBand expected)
    {
        Assert.Equal(expected, RollRules.BandFor(percentage));
    }

    [Fact]
    public void Choose_SupportsCommasAndCrLf()
    {
        var service = new FunService(new FixedRandomSource(1));

        var result = service.Choose("PostgreSQL,\r\nClickHouse\nSQLite");

        Assert.Null(result.Error);
        Assert.Equal(["PostgreSQL", "ClickHouse", "SQLite"], result.Options);
        Assert.Equal("ClickHouse", result.Selected);
    }

    [Theory]
    [InlineData(null, ChoiceError.Empty)]
    [InlineData("", ChoiceError.Empty)]
    [InlineData("one", ChoiceError.TooFew)]
    [InlineData("one,,two", ChoiceError.EmptyOption)]
    [InlineData("one,", ChoiceError.EmptyOption)]
    public void Choose_RejectsInvalidInput(string? raw, ChoiceError expected)
    {
        var result = new FunService(new FixedRandomSource(0)).Choose(raw);

        Assert.Equal(expected, result.Error);
        Assert.Null(result.Selected);
    }

    [Fact]
    public void Choose_RejectsMoreThanFiftyOptions()
    {
        var raw = string.Join(',', Enumerable.Range(1, 51).Select(index => index.ToString()));

        var result = new FunService(new FixedRandomSource(0)).Choose(raw);

        Assert.Equal(ChoiceError.TooMany, result.Error);
    }

    [Fact]
    public void Choose_RejectsOptionLongerThanFiftyCharacters()
    {
        var raw = $"{new string('x', 51)},valid";

        var result = new FunService(new FixedRandomSource(0)).Choose(raw);

        Assert.Equal(ChoiceError.OptionTooLong, result.Error);
    }

    [Theory]
    [InlineData(0, BenAnimationGroup.Primary, 0)]
    [InlineData(46, BenAnimationGroup.Primary, 0)]
    [InlineData(47, BenAnimationGroup.Primary, 1)]
    [InlineData(93, BenAnimationGroup.Primary, 1)]
    [InlineData(94, BenAnimationGroup.Rare, 0)]
    [InlineData(95, BenAnimationGroup.Rare, 0)]
    [InlineData(96, BenAnimationGroup.Rare, 1)]
    [InlineData(97, BenAnimationGroup.Rare, 1)]
    [InlineData(98, BenAnimationGroup.Rare, 2)]
    [InlineData(99, BenAnimationGroup.Rare, 2)]
    public void BenRules_UsesConfigured47_47_2_2_2Weights(
        int draw,
        BenAnimationGroup expectedGroup,
        int expectedIndex)
    {
        var choice = BenRules.Select(draw);

        Assert.Equal(expectedGroup, choice.Group);
        Assert.Equal(expectedIndex, choice.Index);
    }

    [Property(MaxTest = 100)]
    public Property Roll_AlwaysProducesAValidPercentage(NonNegativeInt seed)
    {
        var percentage = seed.Get % 101;
        var result = new FunService(new FixedRandomSource(percentage)).Roll("question");

        return (result.Percentage is >= 0 and <= 100
                && result.FavorableCases is >= 0
                && result.TotalCases is > 0
                && (double)result.FavorableCases.Value / result.TotalCases.Value
                    is >= 0 and <= 1)
            .ToProperty()
            .Label($"percentage={result.Percentage}, fraction={result.FavorableCases}/{result.TotalCases}");
    }

    [Property(MaxTest = 100)]
    public Property Choice_ValidBoundedListsRoundTrip(
        NonNegativeInt rawCount,
        NonNegativeInt rawLength)
    {
        var count = 2 + rawCount.Get % (ChoiceRules.MaxOptions - ChoiceRules.MinOptions + 1);
        var length = 1 + rawLength.Get % ChoiceRules.MaxOptionLength;
        var options = Enumerable.Range(0, count)
            .Select(index => new string((char)('a' + index % 26), length))
            .ToArray();

        var parsed = ChoiceRules.TryParse(string.Join('\n', options), out var actual, out var error);

        return (parsed
                && error is null
                && actual.Count == count
                && actual.All(option => option.Length <= ChoiceRules.MaxOptionLength))
            .ToProperty()
            .Label($"count={count}, length={length}, error={error}");
    }

    [Property(MaxTest = 100)]
    public Property Ben_AlwaysReturnsOneOfFiveConfiguredSlots(NonNegativeInt seed)
    {
        var choice = BenRules.Select(seed.Get % BenRules.TotalWeight);

        return (choice.Group == BenAnimationGroup.Primary
                ? choice.Index is 0 or 1
                : choice.Index is >= 0 and <= 2)
            .ToProperty()
            .Label($"choice={choice}");
    }

    private sealed class FixedRandomSource(int value) : IRandomSource
    {
        public int NextInt(int minInclusive, int maxExclusive) =>
            Math.Clamp(value, minInclusive, maxExclusive - 1);
    }
}
