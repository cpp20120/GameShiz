using ChatAdministration.Application.Parsing;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace CasinoShiz.Tests.ChatAdministration;

public sealed class MuteCommandParserPropertyTests
{
    private static readonly (char Unit, double Seconds)[] Units =
    [
        ('s', 1),
        ('m', 60),
        ('h', 3600),
        ('d', 86400),
        ('w', 604800),
    ];

    [Property(MaxTest = 500)]
    public Property EverySupportedUnitRoundTrips(NonNegativeInt amountSeed, NonNegativeInt unitSeed)
    {
        var amount = 1 + amountSeed.Get % 365;
        var unit = Units[unitSeed.Get % Units.Length];
        var input = $"/mute {amount}{unit.Unit} generated reason";

        var parsed = MuteCommandParser.TryParse(input, out var result, out var error);
        var expected = TimeSpan.FromSeconds(amount * unit.Seconds);

        return (parsed && result is not null && result.Duration == expected && result.Reason == "generated reason")
            .ToProperty()
            .Label($"input={input}, error={error}");
    }

    [Property(MaxTest = 500)]
    public Property ParserNeverAcceptsAnEmptyOrZeroDuration(NonNegativeInt seed)
    {
        var command = seed.Get % 2 == 0 ? "/mute" : "/mute 0m reason";
        var parsed = MuteCommandParser.TryParse(command, out _, out var error);

        return (!parsed && !string.IsNullOrWhiteSpace(error)).ToProperty().Label(command);
    }
}
