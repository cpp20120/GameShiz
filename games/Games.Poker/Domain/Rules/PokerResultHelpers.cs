
namespace Games.Poker.Domain.Rules;

public static class PokerResultHelpers
{
    public static CreateResult Fail(PokerError e) => new(e, "", 0);
    public static JoinResult JoinFail(PokerError e) => new(e, Snapshot: null, 0, 0);
    public static LeaveResult LeaveFail(PokerError e) => new(e, Snapshot: null, TableClosed: false);
    public static StartResult StartFail(PokerError e) => new(e, Snapshot: null);
    public static ActionResult ActionFail(PokerError e) => new(e, Snapshot: null, HandTransition.None, Showdown: null, AutoActorName: null, AutoKind: null);
}
