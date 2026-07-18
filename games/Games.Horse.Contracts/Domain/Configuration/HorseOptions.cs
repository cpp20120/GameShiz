namespace Games.Horse.Domain.Configuration;

public sealed class HorseOptions
{
    public const string SectionName = "Games:horse";

    public int HorseCount { get; init; } = 4;
    public int MinBetsToRun { get; init; } = 4;

    /// <summary>Millis after the result GIF before posting winner payouts (groups with more lanes).</summary>
    public int AnnounceDelayMs { get; init; } = 20_000;

    /// <summary>
    /// Same as <see cref="AnnounceDelayMs"/> but when <see cref="HorseCount"/> is 2. The race GIF often runs
    /// longer than the default delay; this keeps the win message from overtaking playback.
    /// </summary>
    public int AnnounceDelay1V1Ms { get; init; } = 33_000;

    /// <summary>Number of deterministic visual GIF variants pre-rendered for each possible winner.</summary>
    public int RenderVariants { get; init; } = 3;

    /// <summary>Hours east of UTC for race calendar day and for interpreting <see cref="AutoRunLocalHour"/> / <see cref="AutoRunLocalMinute"/>.</summary>
    public int TimezoneOffsetHours { get; init; } = 7;

    /// <summary>When true, a background job runs one global race per calendar day after the configured local time (if <see cref="MinBetsToRun"/> is met).</summary>
    public bool AutoRunEnabled { get; init; }

    /// <summary>Run the scheduled race every N calendar days. A value of 1 means every day.</summary>
    public int AutoRunEveryDays { get; init; } = 1;

    /// <summary>Local wall-clock hour (0–23) in <see cref="TimezoneOffsetHours"/> after which the auto-run may fire.</summary>
    public int AutoRunLocalHour { get; init; } = 21;

    /// <summary>Local wall-clock minute (0–59).</summary>
    public int AutoRunLocalMinute { get; init; }

    public List<long> Admins { get; init; } = [];
}
