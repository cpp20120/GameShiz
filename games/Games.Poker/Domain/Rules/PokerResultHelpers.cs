using Games.Poker.Domain;

namespace Games.Poker;

public static class PokerResultHelpers
{
    public static CreateResult Fail(PokerError e) => new(e, "", 0);
    public static JoinResult JoinFail(PokerError e) => new(e, null, 0, 0);
    public static LeaveResult LeaveFail(PokerError e) => new(e, null, false);
    public static StartResult StartFail(PokerError e) => new(e, null);
    public static ActionResult ActionFail(PokerError e) => new(e, null, HandTransition.None, null, null, null);
}
