namespace Games.Discord.Shared;

public static class DiscordGameFormatting
{
    public static string Daily(int used, int limit) => limit > 0
        ? $"Attempts: {Math.Max(0, limit - used)}/{limit} remaining"
        : string.Empty;

    public static string NativeResult(string emoji, int face, int bet, int multiplier, int payout, int balance, int used, int limit)
    {
        var net = payout - bet;
        var outcome = net > 0 ? $"win +{net}" : net < 0 ? $"loss {-net}" : "break-even";
        var daily = Daily(used, limit);
        return string.Join('\n', new[]
        {
            $"{emoji} face **{face}** — {outcome}",
            $"Bet: **{bet}**, multiplier: **x{multiplier}**, payout: **{payout}**",
            $"Balance: **{balance}**",
            daily,
        }.Where(static line => line.Length > 0));
    }

    public static string BetError(string game, object error, int balance = 0, int pending = 0, string? blockingGame = null, int cooldown = 0)
    {
        var details = new List<string> { $"{game}: **{error}**" };
        if (balance != 0) details.Add($"Balance: **{balance}**");
        if (pending != 0) details.Add($"Pending bet: **{pending}**");
        if (!string.IsNullOrWhiteSpace(blockingGame)) details.Add($"Finish **{blockingGame}** first.");
        if (cooldown > 0) details.Add($"Retry in **{cooldown}s**.");
        return string.Join('\n', details);
    }
}
