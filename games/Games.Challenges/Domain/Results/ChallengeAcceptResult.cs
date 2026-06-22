namespace Games.Challenges;

public sealed record ChallengeAcceptResult(
    ChallengeAcceptError Error,
    Challenge? Challenge = null,
    int ChallengerRoll = 0,
    int TargetRoll = 0,
    long WinnerId = 0,
    string WinnerName = "",
    int Payout = 0,
    int Fee = 0,
    bool IsTie = false);
