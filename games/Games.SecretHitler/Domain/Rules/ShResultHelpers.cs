
namespace Games.SecretHitler.Domain.Rules;

public static class ShResultHelpers
{
    public static ShCreateResult CreateFail(ShError e) => new(e, "", 0);
    public static ShJoinResult JoinFail(ShError e) => new(e, Snapshot: null, 0, 0);
    public static ShLeaveResult LeaveFail(ShError e) => new(e, Snapshot: null, GameClosed: false);
    public static ShStartResult StartFail(ShError e) => new(e, Snapshot: null);
    public static ShNominateResult NominateFail(ShError e) => new(e, Snapshot: null);
    public static ShVoteResult VoteFail(ShError e) => new(e, Snapshot: null, After: null);
    public static ShDiscardResult DiscardFail(ShError e) => new(e, Snapshot: null);
    public static ShEnactResult EnactFail(ShError e) => new(e, Snapshot: null, After: null);

    public static ShError MapValidation(ShValidation v) => v switch
    {
        ShValidation.NotPresident => ShError.NotPresident,
        ShValidation.NotChancellor => ShError.NotChancellor,
        ShValidation.NotYourTurn => ShError.WrongPhase,
        ShValidation.InvalidTarget => ShError.InvalidTarget,
        ShValidation.TermLimited => ShError.TermLimited,
        ShValidation.AlreadyVoted => ShError.AlreadyVoted,
        ShValidation.WrongPhase => ShError.WrongPhase,
        ShValidation.InvalidPolicy => ShError.InvalidPolicy,
        _ => ShError.None,
    };
}
