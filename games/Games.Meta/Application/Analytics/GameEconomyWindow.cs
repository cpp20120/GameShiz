namespace Games.Meta.Application.Analytics;

internal sealed record GameEconomyWindow(string Module, long Rows, long Stake, long Payout, long Net, long Users);
