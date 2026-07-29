namespace Games.Fun.Domain;

public static class RollRules
{
    public static RollOutcome Create(int percentage, string? question)
    {
        if (percentage is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(percentage));

        var normalizedQuestion = string.IsNullOrWhiteSpace(question) ? null : question.Trim();
        if (normalizedQuestion is null)
            return new RollOutcome(null, percentage, null, null, BandFor(percentage));

        const int baseCases = 100;
        var favorable = (int)Math.Round(percentage * baseCases / 100d, MidpointRounding.AwayFromZero);
        var divisor = GreatestCommonDivisor(favorable, baseCases);
        return new RollOutcome(
            normalizedQuestion,
            percentage,
            favorable / divisor,
            baseCases / divisor,
            BandFor(percentage));
    }

    public static RollBand BandFor(int percentage) => percentage switch
    {
        < 20 => RollBand.VeryUnlikely,
        < 40 => RollBand.Unlikely,
        < 60 => RollBand.Uncertain,
        < 80 => RollBand.Likely,
        _ => RollBand.VeryLikely,
    };

    private static int GreatestCommonDivisor(int left, int right)
    {
        while (right != 0)
            (left, right) = (right, left % right);

        return Math.Abs(left) is 0 ? 1 : Math.Abs(left);
    }
}
